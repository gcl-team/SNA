using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using SimNextgenApp.Observability;

namespace SimNextgenApp.Tests.Observability;

public class MetricCardinalityLimitTests
{
    private const string OverflowTag = "otel.metric.overflow";

    [Fact(DisplayName = "WithMetricCardinalityLimit should reject zero or negative limits.")]
    public void WithMetricCardinalityLimit_ZeroOrNegative_ThrowsArgumentOutOfRangeException()
    {
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationTelemetry.Create().WithMetricCardinalityLimit(0));
        Assert.Equal("limit", ex1.ParamName);

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationTelemetry.Create().WithMetricCardinalityLimit(-1));
        Assert.Equal("limit", ex2.ParamName);
    }

    [Fact(DisplayName = "Build should cap SNA metric streams at the default cardinality limit.")]
    public void Build_DefaultLimit_OverflowsBeyondDefault()
    {
        var points = RecordDistinctTagValues(
            builder => builder,
            SimulationTelemetryBuilder.DefaultMetricCardinalityLimit + 10);

        Assert.Contains(points, HasOverflowTag);
        Assert.Equal(SimulationTelemetryBuilder.DefaultMetricCardinalityLimit, points.Count(p => !HasOverflowTag(p)));
    }

    [Fact(DisplayName = "WithMetricCardinalityLimit should cap SNA metric streams at the given limit.")]
    public void WithMetricCardinalityLimit_CustomLimit_OverflowsBeyondLimit()
    {
        var points = RecordDistinctTagValues(builder => builder.WithMetricCardinalityLimit(5), 10);

        Assert.Contains(points, HasOverflowTag);
        Assert.Equal(5, points.Count(p => !HasOverflowTag(p)));
    }

    [Fact(DisplayName = "WithMetricCardinalityLimit(null) should leave SNA metric streams at the SDK default.")]
    public void WithMetricCardinalityLimit_Null_UsesSdkDefault()
    {
        int distinctValues = SimulationTelemetryBuilder.DefaultMetricCardinalityLimit + 10;

        var points = RecordDistinctTagValues(builder => builder.WithMetricCardinalityLimit(null), distinctValues);

        Assert.DoesNotContain(points, HasOverflowTag);
        Assert.Equal(distinctValues, points.Count);
    }

    [Fact(DisplayName = "WithMetricCardinalityLimit(null) should let a user view be the only stream for an SNA instrument.")]
    public void WithMetricCardinalityLimit_NullWithUserView_ExportsSingleStream()
    {
        string instrumentName = UniqueInstrumentName();
        var exportedMetrics = new List<string>();

        using (var telemetry = SimulationTelemetry.Create()
            .WithMetricCardinalityLimit(null)
            .ConfigureOpenTelemetry(configureMeter: meter => meter
                .AddView(instrumentName, new MetricStreamConfiguration { CardinalityLimit = 5 })
                .AddReader(new BaseExportingMetricReader(new CollectingMetricExporter(exportedMetrics, instrumentName))))
            .Build())
        using (var meter = new Meter(SimulationTelemetry.MeterName))
        {
            meter.CreateCounter<int>(instrumentName).Add(1);
            telemetry.Flush();
        }

        Assert.Single(exportedMetrics);
    }

    [Fact(DisplayName = "The cardinality limit view should keep histogram aggregation for SNA histograms.")]
    public void Build_DefaultLimit_KeepsHistogramAggregation()
    {
        string instrumentName = UniqueInstrumentName();
        var metricTypes = new List<MetricType>();

        using (var telemetry = SimulationTelemetry.Create()
            .ConfigureOpenTelemetry(configureMeter: meter => meter
                .AddReader(new BaseExportingMetricReader(new CollectingMetricExporter(metricTypes, instrumentName))))
            .Build())
        using (var meter = new Meter(SimulationTelemetry.MeterName))
        {
            meter.CreateHistogram<double>(instrumentName).Record(1.5);
            telemetry.Flush();
        }

        Assert.Equal([MetricType.Histogram], metricTypes);
    }

    [Fact(DisplayName = "The cardinality limit view should not apply to meters outside SNA.")]
    public void Build_DefaultLimit_DoesNotApplyToOtherMeters()
    {
        string meterName = $"Other.{Guid.NewGuid():N}";
        string instrumentName = UniqueInstrumentName();
        int distinctValues = SimulationTelemetryBuilder.DefaultMetricCardinalityLimit + 10;
        var points = new List<MetricPoint>();

        using (var telemetry = SimulationTelemetry.Create()
            .ConfigureOpenTelemetry(configureMeter: meter => meter
                .AddMeter(meterName)
                .AddReader(new BaseExportingMetricReader(new CollectingMetricExporter(points, instrumentName))))
            .Build())
        using (var meter = new Meter(meterName))
        {
            var counter = meter.CreateCounter<int>(instrumentName);
            for (int i = 0; i < distinctValues; i++)
                counter.Add(1, new KeyValuePair<string, object?>("value", i));
            telemetry.Flush();
        }

        Assert.DoesNotContain(points, HasOverflowTag);
    }

    /// <summary>
    /// Records one measurement for each of <paramref name="distinctValues"/> tag values on a counter
    /// of the SNA meter, flushes, and returns the exported data points.
    /// </summary>
    private static List<MetricPoint> RecordDistinctTagValues(
        Func<SimulationTelemetryBuilder, SimulationTelemetryBuilder> configure,
        int distinctValues)
    {
        // Other tests may publish on the shared SNA meter name at the same time, so only this
        // test's uniquely named instrument is collected.
        string instrumentName = UniqueInstrumentName();
        var points = new List<MetricPoint>();

        using var telemetry = configure(SimulationTelemetry.Create())
            .ConfigureOpenTelemetry(configureMeter: meter => meter
                .AddReader(new BaseExportingMetricReader(new CollectingMetricExporter(points, instrumentName))))
            .Build();
        using var meter = new Meter(SimulationTelemetry.MeterName);

        var counter = meter.CreateCounter<int>(instrumentName);
        for (int i = 0; i < distinctValues; i++)
            counter.Add(1, new KeyValuePair<string, object?>("value", i));
        telemetry.Flush();

        return points;
    }

    private static string UniqueInstrumentName() => $"test.cardinality.{Guid.NewGuid():N}";

    private static bool HasOverflowTag(MetricPoint point)
    {
        foreach (var tag in point.Tags)
        {
            if (tag.Key == OverflowTag)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Copies what the tests need out of each exported metric with the given name. Metric objects
    /// are reused by the SDK after export, so they cannot be kept and inspected later.
    /// Only the first export is kept, because disposing the telemetry collects and exports again.
    /// </summary>
    private sealed class CollectingMetricExporter : BaseExporter<Metric>
    {
        private readonly string _instrumentName;
        private readonly Action<Metric> _collect;
        private bool _exported;

        public CollectingMetricExporter(List<MetricPoint> points, string instrumentName)
            : this(instrumentName, metric =>
            {
                foreach (ref readonly var point in metric.GetMetricPoints())
                    points.Add(point);
            })
        {
        }

        public CollectingMetricExporter(List<MetricType> metricTypes, string instrumentName)
            : this(instrumentName, metric => metricTypes.Add(metric.MetricType))
        {
        }

        public CollectingMetricExporter(List<string> metricNames, string instrumentName)
            : this(instrumentName, metric => metricNames.Add(metric.Name))
        {
        }

        private CollectingMetricExporter(string instrumentName, Action<Metric> collect)
        {
            _instrumentName = instrumentName;
            _collect = collect;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            if (_exported)
                return ExportResult.Success;
            _exported = true;

            foreach (var metric in batch)
            {
                if (metric.Name == _instrumentName)
                    _collect(metric);
            }
            return ExportResult.Success;
        }
    }
}
