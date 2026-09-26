using Microsoft.Extensions.Logging.Abstractions;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Modeling;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Observability;

namespace SimNextgenApp.Benchmarks;

internal sealed class BenchmarkLoad;

/// <summary>
/// A small M/M/c queueing model (Generator -> Queue -> Servers) with no logging,
/// so benchmarks measure the engine and telemetry rather than log formatting.
/// </summary>
internal sealed class BenchmarkModel : AbstractSimulationModel
{
    private readonly Generator<BenchmarkLoad> _generator;
    private readonly SimQueue<BenchmarkLoad> _queue;
    private readonly List<Server<BenchmarkLoad>> _servers = [];
    private readonly List<ServerObserver<BenchmarkLoad>> _serverObservers = [];
    private readonly QueueObserver<BenchmarkLoad>? _queueObserver;
    private IRunContext _runContext = null!;

    /// <param name="telemetry">
    /// When provided, observers are created through it so they share its volume estimator,
    /// matching how an application wires observers; otherwise standalone observers are used.
    /// </param>
    /// <param name="attachObservers">
    /// Whether to attach queue and server observers. Observers also collect result statistics,
    /// so they can be present even when telemetry is off.
    /// </param>
    public BenchmarkModel(SimulationTelemetry? telemetry, bool attachObservers, int numberOfServers = 2, int seed = 42)
        : base("BenchmarkModel")
    {
        var loggerFactory = NullLoggerFactory.Instance;

        // Mean inter-arrival 1s, mean service 1.5s per server: busy but stable with 2 servers.
        var generatorConfig = new GeneratorStaticConfig<BenchmarkLoad>(
            rnd => TimeSpan.FromSeconds(-1.0 * Math.Log(1.0 - rnd.NextDouble())),
            _ => new BenchmarkLoad())
        { IsSkippingFirst = false };
        var serverConfig = new ServerStaticConfig<BenchmarkLoad>(
            (_, rnd) => TimeSpan.FromSeconds(-1.5 * Math.Log(1.0 - rnd.NextDouble())))
        { Capacity = 1 };

        _generator = new Generator<BenchmarkLoad>(generatorConfig, seed, "Arrivals", loggerFactory);
        _queue = new SimQueue<BenchmarkLoad>(new QueueStaticConfig<BenchmarkLoad>(), "Queue", loggerFactory);
        if (attachObservers)
            _queueObserver = telemetry?.ObserveQueue(_queue) ?? QueueObserver.CreateSimple(_queue);

        for (int i = 0; i < numberOfServers; i++)
        {
            var server = new Server<BenchmarkLoad>(serverConfig, seed + i + 1, $"Server{i + 1}");
            server.LoadDeparted += (_, _) => _queue.TriggerDequeueAttempt(_runContext);
            _servers.Add(server);
            if (attachObservers)
                _serverObservers.Add(telemetry?.ObserveServer(server) ?? ServerObserver.CreateSimple(server));
        }

        _generator.LoadGenerated += (load, _) =>
        {
            var idleServer = _servers.FirstOrDefault(s => s.Vacancy > 0);
            if (idleServer != null)
                idleServer.TryStartService(load, _runContext);
            else
                _queue.TryScheduleEnqueue(load, _runContext);
        };

        _queue.LoadDequeued += (load, _) =>
            _servers.FirstOrDefault(s => s.Vacancy > 0)?.TryStartService(load, _runContext);
    }

    public override void Initialize(IRunContext runContext)
    {
        _runContext = runContext;
        _queueObserver?.SetTimeUnit(runContext.TimeUnit);
        foreach (var observer in _serverObservers)
            observer.SetTimeUnit(runContext.TimeUnit);

        _generator.Initialize(runContext);
        _queue.Initialize(runContext);
    }

    public override void WarmedUp(long simulationTime)
    {
        _generator.WarmedUp(simulationTime);
        _queue.WarmedUp(simulationTime);
        foreach (var server in _servers)
            server.WarmedUp(simulationTime);
    }

    /// <summary>
    /// Disposes the observers so their meters stop publishing into the next run.
    /// </summary>
    public void DisposeObservers()
    {
        _queueObserver?.Dispose();
        foreach (var observer in _serverObservers)
            observer.Dispose();
    }
}
