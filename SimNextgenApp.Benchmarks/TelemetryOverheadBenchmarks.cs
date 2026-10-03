using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Observability;

namespace SimNextgenApp.Benchmarks;

/// <summary>
/// Measures the per-event cost of telemetry on the simulation thread.
/// Each benchmark runs the same model for <see cref="EventCount"/> events; results are reported per event.
/// </summary>
/// <remarks>
/// <para>
/// BenchmarkDotNet runs each benchmark in its own process, so the telemetry built for one case
/// (and the listeners it registers on the shared ActivitySource and Meter) never leaks into another.
/// </para>
/// <para>
/// <see cref="SimulationEngine.Run"/> always calls <see cref="SimulationTelemetry.Flush"/> after
/// initialization and at the end of the run, on the simulation thread. That and the observers' metric
/// streams are a fixed per-run cost (measured by <see cref="TelemetryPerRunCostBenchmarks"/>), so
/// <see cref="EventCount"/> is large enough to amortize it and keep the per-event numbers close to steady state.
/// </para>
/// <para>
/// Building the model, its observers (and their meters and instruments) and the engine happens in
/// <see cref="IterationSetup"/>, outside the measured region, so it does not count towards Mean or
/// Allocated. An engine runs only once, so each iteration is a single invocation. Work inside
/// <see cref="SimulationEngine.Run"/> itself, such as model initialization, is still measured.
/// Because MemoryDiagnoser takes its GC counts from one extra iteration, <see cref="EventCount"/>
/// is also what gives those counts their resolution.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[InvocationCount(1)]
public class TelemetryOverheadBenchmarks
{
    private const int EventCount = 1_000_000;

    private SimulationTelemetry? _telemetry;
    private bool _attachObservers = true;
    private BenchmarkModel? _model;
    private SimulationEngine? _engine;

    [GlobalSetup(Target = nameof(EngineOnly))]
    public void SetupEngineOnly()
    {
        _telemetry = null;
        _attachObservers = false;
    }

    [GlobalSetup(Target = nameof(ObserversNoTelemetry))]
    public void SetupObserversNoTelemetry() => _telemetry = null;

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

    [IterationSetup]
    public void BuildSimulation()
    {
        _model = new BenchmarkModel(_telemetry, _attachObservers);
        var profile = new SimulationProfile(
            _model,
            new EventCountRunStrategy(EventCount),
            name: "Benchmark",
            telemetry: _telemetry);
        _engine = new SimulationEngine(profile);
    }

    [IterationCleanup]
    public void DisposeSimulation()
    {
        _model?.DisposeObservers();
        _model = null;
        _engine = null;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = EventCount, Description = "Engine only")]
    public long EngineOnly() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "Observers, no telemetry")]
    public long ObserversNoTelemetry() => RunSimulation();

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
        _engine!.Run();
        return _engine.ExecutedEventCount;
    }

    /// <summary>
    /// Adds exporters that use the same batching/periodic processors as the OTLP exporter,
    /// but drop the data instead of sending it over the network.
    /// </summary>
    internal static SimulationTelemetryBuilder WithDiscardingExporters(SimulationTelemetryBuilder builder) =>
        builder.ConfigureOpenTelemetry(
            configureTracer: tracer => tracer.AddProcessor(
                new BatchActivityExportProcessor(new DiscardingActivityExporter())),
            configureMeter: meter => meter.AddReader(
                new PeriodicExportingMetricReader(new DiscardingMetricExporter())));
}
