using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Observability;

namespace SimNextgenApp.Benchmarks;

/// <summary>
/// Measures the per-event cost of telemetry on the simulation thread (see issue #78).
/// Each benchmark runs the same model for <see cref="EventCount"/> events; results are reported per event.
/// </summary>
/// <remarks>
/// BenchmarkDotNet runs each benchmark in its own process, so the telemetry built for one case
/// (and the listeners it registers on the shared ActivitySource and Meter) never leaks into another.
/// </remarks>
[MemoryDiagnoser]
public class TelemetryOverheadBenchmarks
{
    private const int EventCount = 10_000;

    private SimulationTelemetry? _telemetry;

    [GlobalSetup(Target = nameof(NoTelemetry))]
    public void SetupNoTelemetry() => _telemetry = null;

    [GlobalSetup(Target = nameof(TelemetryNoExporter))]
    public void SetupTelemetryNoExporter() =>
        _telemetry = SimulationTelemetry.Create().Build();

    [GlobalSetup(Target = nameof(BatchExporter))]
    public void SetupBatchExporter() =>
        _telemetry = WithDiscardingExporters(SimulationTelemetry.Create()).Build();

    [GlobalSetup(Target = nameof(TraceContext))]
    public void SetupTraceContext() =>
        _telemetry = WithDiscardingExporters(SimulationTelemetry.Create()).WithTraceContext().Build();

    [GlobalSetup(Target = nameof(VolumeAndCardinalityGuards))]
    public void SetupVolumeAndCardinalityGuards() =>
        _telemetry = WithDiscardingExporters(SimulationTelemetry.Create())
            .WithVolumeEstimation()
            .WithCardinalityGuard()
            .Build();

    [GlobalSetup(Target = nameof(Sampling10Percent))]
    public void SetupSampling10Percent() =>
        _telemetry = WithDiscardingExporters(SimulationTelemetry.Create()).WithSampling(0.1).Build();

    [GlobalCleanup]
    public void Cleanup() => _telemetry?.Dispose();

    [Benchmark(Baseline = true, OperationsPerInvoke = EventCount, Description = "No telemetry")]
    public long NoTelemetry() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "Telemetry, no exporter")]
    public long TelemetryNoExporter() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "+ Batch exporter")]
    public long BatchExporter() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "+ WithTraceContext")]
    public long TraceContext() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "+ Volume & cardinality guards")]
    public long VolumeAndCardinalityGuards() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "+ Sampling 10%")]
    public long Sampling10Percent() => RunSimulation();

    private long RunSimulation()
    {
        var model = new BenchmarkModel(_telemetry);
        try
        {
            var profile = new SimulationProfile(
                model,
                new EventCountRunStrategy(EventCount),
                name: "Benchmark",
                telemetry: _telemetry);

            var engine = new SimulationEngine(profile);
            engine.Run();
            return engine.ExecutedEventCount;
        }
        finally
        {
            model.DisposeObservers();
        }
    }

    /// <summary>
    /// Adds exporters that use the same batching/periodic processors as the OTLP exporter,
    /// but drop the data instead of sending it over the network.
    /// </summary>
    private static SimulationTelemetryBuilder WithDiscardingExporters(SimulationTelemetryBuilder builder) =>
        builder.ConfigureOpenTelemetry(
            configureTracer: tracer => tracer.AddProcessor(
                new BatchActivityExportProcessor(new DiscardingActivityExporter())),
            configureMeter: meter => meter.AddReader(
                new PeriodicExportingMetricReader(new DiscardingMetricExporter())));
}
