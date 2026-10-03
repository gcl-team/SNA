using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Observability;

namespace SimNextgenApp.Benchmarks;

/// <summary>
/// Measures how the span processor's settings affect Gen 1 collections in a long run.
/// </summary>
/// <remarks>
/// <para>
/// A batch processor keeps spans alive until its export thread drains them, so spans that are
/// still queued when a Gen 0 collection happens get promoted to Gen 1. The cases vary how many
/// spans can wait for export, and compare against a simple (synchronous) processor, which ends
/// each span's life on the simulation thread.
/// </para>
/// <para>
/// Only traces are configured, so metric storage does not add to the GC counts. The setup follows
/// <see cref="TelemetryOverheadBenchmarks"/>: one invocation per iteration, and the model is built
/// outside the measured region.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[InvocationCount(1)]
public class BatchProcessorTuningBenchmarks
{
    private const int EventCount = 1_000_000;

    private SimulationTelemetry? _telemetry;
    private readonly DiscardingActivityExporter _exporter = new();
    private long _runs;
    private BenchmarkModel? _model;
    private SimulationEngine? _engine;

    [GlobalSetup(Target = nameof(BatchDefault))]
    public void SetupBatchDefault() =>
        _telemetry = WithSpanProcessor(new BatchActivityExportProcessor(_exporter));

    [GlobalSetup(Target = nameof(BatchSmall))]
    public void SetupBatchSmall() =>
        _telemetry = WithSpanProcessor(new BatchActivityExportProcessor(
            _exporter, maxQueueSize: 2048, maxExportBatchSize: 64));

    [GlobalSetup(Target = nameof(BatchTiny))]
    public void SetupBatchTiny() =>
        _telemetry = WithSpanProcessor(new BatchActivityExportProcessor(
            _exporter, maxQueueSize: 256, maxExportBatchSize: 16));

    [GlobalSetup(Target = nameof(Simple))]
    public void SetupSimple() =>
        _telemetry = WithSpanProcessor(new SimpleActivityExportProcessor(_exporter));

    [GlobalCleanup]
    public void Cleanup()
    {
        _telemetry?.Dispose();
        // Shows up in the BenchmarkDotNet log; a value well below the spans per run means spans were dropped.
        Console.WriteLine($"// Exported spans per run: {_exporter.ExportedCount / Math.Max(_runs, 1):N0}");
    }

    [IterationSetup]
    public void BuildSimulation()
    {
        _model = new BenchmarkModel(_telemetry, attachObservers: true);
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

    [Benchmark(Baseline = true, OperationsPerInvoke = EventCount, Description = "Batch: queue 2048, batch 512 (default)")]
    public long BatchDefault() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "Batch: queue 2048, batch 64")]
    public long BatchSmall() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "Batch: queue 256, batch 16")]
    public long BatchTiny() => RunSimulation();

    [Benchmark(OperationsPerInvoke = EventCount, Description = "Simple (synchronous)")]
    public long Simple() => RunSimulation();

    private long RunSimulation()
    {
        _engine!.Run();
        _runs++;
        return _engine.ExecutedEventCount;
    }

    private SimulationTelemetry WithSpanProcessor(BaseProcessor<System.Diagnostics.Activity> processor) =>
        SimulationTelemetry.Create()
            .ConfigureOpenTelemetry(configureTracer: tracer => tracer.AddProcessor(processor))
            .Build();
}
