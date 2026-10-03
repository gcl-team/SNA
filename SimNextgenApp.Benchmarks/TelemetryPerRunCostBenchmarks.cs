using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Observability;

namespace SimNextgenApp.Benchmarks;

/// <summary>
/// Measures the fixed cost that telemetry adds to each simulation run, independent of its length.
/// This is the cost that dominates workloads with many short runs, such as parameter sweeps.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="TelemetryOverheadBenchmarks"/>, the "short run" cases build the model, its observers
/// and the engine inside the measured region, then dispose the observers, so a whole run is one operation.
/// The telemetry itself is built once per process and shared across runs, as an application running a
/// sweep would do.
/// </para>
/// <para>
/// The "traces only" and "metrics only" cases split that cost by signal. The metrics cases compare SNA's
/// default metric cardinality limit with the SDK default of 2000 points per stream: the SDK allocates
/// storage for every point up front, and because observers dispose their meters, it does so on every run.
/// </para>
/// <para>
/// The remaining cases isolate parts of the fixed cost: creating and disposing the observers (with and
/// without a collect in between, which is what frees the old metric streams), and
/// <see cref="SimulationTelemetry.Flush"/>, which <see cref="SimulationEngine.Run"/> calls on the
/// simulation thread after initialization and at the end.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class TelemetryPerRunCostBenchmarks
{
    private const int ShortRunEventCount = 1_000;
    private const int NumberOfServers = 2;

    private SimulationTelemetry? _telemetry;
    private bool _attachObservers = true;
    private SimQueue<BenchmarkLoad> _queue = null!;
    private readonly List<Server<BenchmarkLoad>> _servers = [];

    [GlobalSetup(Targets = [nameof(ShortRunEngineOnly)])]
    public void SetupEngineOnly()
    {
        _telemetry = null;
        _attachObservers = false;
    }

    [GlobalSetup(Targets = [nameof(ShortRunObserversNoTelemetry)])]
    public void SetupNoTelemetry() => _telemetry = null;

    [GlobalSetup(Targets = [nameof(ShortRunTelemetryNoExporter)])]
    public void SetupTelemetryNoExporter() =>
        _telemetry = SimulationTelemetry.Create().Build();

    [GlobalSetup(Targets = [nameof(ShortRunBatchExporter), nameof(CreateAndDisposeObservers), nameof(CreateFlushDisposeObservers), nameof(Flush)])]
    public void SetupBatchExporter()
    {
        _telemetry = TelemetryOverheadBenchmarks.WithDiscardingExporters(SimulationTelemetry.Create()).Build();

        // Components for the observer-only case; observers subscribe to them but never see an event.
        _queue = new SimQueue<BenchmarkLoad>(new QueueStaticConfig<BenchmarkLoad>(), "Queue", NullLoggerFactory.Instance);
        var serverConfig = new ServerStaticConfig<BenchmarkLoad>((_, _) => TimeSpan.FromSeconds(1)) { Capacity = 1 };
        for (int i = 0; i < NumberOfServers; i++)
            _servers.Add(new Server<BenchmarkLoad>(serverConfig, i + 1, $"Server{i + 1}"));
    }

    [GlobalSetup(Targets = [nameof(ShortRunTracesOnly)])]
    public void SetupTracesOnly() =>
        _telemetry = SimulationTelemetry.Create()
            .ConfigureOpenTelemetry(configureTracer: tracer => tracer.AddProcessor(
                new BatchActivityExportProcessor(new DiscardingActivityExporter())))
            .Build();

    [GlobalSetup(Targets = [nameof(ShortRunMetricsOnly)])]
    public void SetupMetricsOnly() =>
        _telemetry = SimulationTelemetry.Create()
            .ConfigureOpenTelemetry(configureMeter: meter => meter.AddReader(
                new PeriodicExportingMetricReader(new DiscardingMetricExporter())))
            .Build();

    [GlobalSetup(Targets = [nameof(ShortRunMetricsSdkDefaultLimit)])]
    public void SetupMetricsSdkDefaultLimit() =>
        _telemetry = SimulationTelemetry.Create()
            .WithMetricCardinalityLimit(null)
            .ConfigureOpenTelemetry(configureMeter: meter => meter.AddReader(
                new PeriodicExportingMetricReader(new DiscardingMetricExporter())))
            .Build();

    [GlobalCleanup]
    public void Cleanup() => _telemetry?.Dispose();

    [Benchmark(Baseline = true, Description = "Short run: engine only")]
    public long ShortRunEngineOnly() => RunShortSimulation();

    [Benchmark(Description = "Short run: observers, no telemetry")]
    public long ShortRunObserversNoTelemetry() => RunShortSimulation();

    [Benchmark(Description = "Short run: telemetry, no exporter")]
    public long ShortRunTelemetryNoExporter() => RunShortSimulation();

    [Benchmark(Description = "Short run: + batch exporter")]
    public long ShortRunBatchExporter() => RunShortSimulation();

    [Benchmark(Description = "Short run: traces only (batch)")]
    public long ShortRunTracesOnly() => RunShortSimulation();

    [Benchmark(Description = "Short run: metrics only (reader)")]
    public long ShortRunMetricsOnly() => RunShortSimulation();

    [Benchmark(Description = "Short run: metrics only, SDK default limit")]
    public long ShortRunMetricsSdkDefaultLimit() => RunShortSimulation();

    [Benchmark(Description = "Create + dispose observers (batch exporter)")]
    public void CreateAndDisposeObservers()
    {
        var queueObserver = _telemetry!.ObserveQueue(_queue);
        var serverObservers = new ServerObserver<BenchmarkLoad>[NumberOfServers];
        for (int i = 0; i < NumberOfServers; i++)
            serverObservers[i] = _telemetry.ObserveServer(_servers[i]);

        queueObserver.Dispose();
        foreach (var observer in serverObservers)
            observer.Dispose();
    }

    [Benchmark(Description = "Create + flush + dispose + flush observers (batch exporter)")]
    public void CreateFlushDisposeObservers()
    {
        var queueObserver = _telemetry!.ObserveQueue(_queue);
        var serverObservers = new ServerObserver<BenchmarkLoad>[NumberOfServers];
        for (int i = 0; i < NumberOfServers; i++)
            serverObservers[i] = _telemetry.ObserveServer(_servers[i]);
        _telemetry.Flush();

        queueObserver.Dispose();
        foreach (var observer in serverObservers)
            observer.Dispose();
        _telemetry.Flush();
    }

    [Benchmark(Description = "Flush, nothing pending (batch exporter)")]
    public bool Flush() => _telemetry!.Flush();

    private long RunShortSimulation()
    {
        var model = new BenchmarkModel(_telemetry, _attachObservers, NumberOfServers);
        var profile = new SimulationProfile(
            model,
            new EventCountRunStrategy(ShortRunEventCount),
            name: "Benchmark",
            telemetry: _telemetry);
        var engine = new SimulationEngine(profile);
        engine.Run();
        model.DisposeObservers();
        return engine.ExecutedEventCount;
    }
}
