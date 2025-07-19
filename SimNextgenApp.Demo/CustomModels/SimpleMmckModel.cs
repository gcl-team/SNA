using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Modeling;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Demo.CustomModels;

internal class SimpleMmckModel : AbstractSimulationModel
{
    public Generator<MyLoad> LoadGenerator { get; }
    public SimQueue<MyLoad> WaitingLine { get; }
    public List<Server<MyLoad>> ServiceChannels { get; }
    public List<ServerObserver<MyLoad>> ServiceChannelObservers { get; }

    private readonly ILogger<SimpleMmckModel> _modelLogger;
    private IRunContext _runContext = null!;

    public int SystemCapacityK { get; }
    public int NumberOfServersC => ServiceChannels.Count;

    // Statistics for the system
    public int BalkedLoadsCount { get; private set; } = 0;
    public int TotalLoadsEnteredSystem { get; private set; } = 0;

    public SimpleMmckModel(
        GeneratorStaticConfig<MyLoad> genConfig, int genSeed,
        QueueStaticConfig<MyLoad> queueConfig, // Queue config now used
        ServerStaticConfig<MyLoad> serverConfigTemplate, int numberOfServers, int serverSeedBase,
        int systemCapacityK, // Overall system capacity K
        ILoggerFactory loggerFactory, string name = "MMCK_System")
        : base(name)
    {
        _modelLogger = loggerFactory.CreateLogger<SimpleMmckModel>();

        if (numberOfServers <= 0)
            throw new ArgumentOutOfRangeException(nameof(numberOfServers), "Number of servers (c) must be positive.");
        if (systemCapacityK < numberOfServers)
            throw new ArgumentException("System capacity (K) cannot be less than the number of servers (c).", nameof(systemCapacityK));

        SystemCapacityK = systemCapacityK;

        // 1. Create Generator
        LoadGenerator = new Generator<MyLoad>(genConfig, genSeed, $"{name}_Arrivals", loggerFactory);

        // 2. Create Queue
        // Queue capacity is K - c (waiting spots)
        // If K == c, queue capacity is 0 (no waiting room - loss system if servers are busy)
        var actualQueueConfig = queueConfig with { Capacity = SystemCapacityK - numberOfServers };
        WaitingLine = new SimQueue<MyLoad>(actualQueueConfig, $"{name}_Queue", loggerFactory);

        // 3. Create Servers and Server Observers
        ServiceChannels = [];
        ServiceChannelObservers = [];
        for (int i = 0; i < numberOfServers; i++)
        {
            // Each server uses the same config template but can have a different seed
            ServiceChannels.Add(new Server<MyLoad>(
                serverConfigTemplate,
                serverSeedBase + i, // Different seed for each server
                $"{name}_Server{i + 1}"
            ));

            // Create an observer for each server
            ServiceChannelObservers.Add(new ServerObserver<MyLoad>(ServiceChannels[i]));
        }

        foreach (var server in ServiceChannels)
        {
            server.LoadDeparted += (departedLoad, departureTime) =>
            {
                _modelLogger.LogInformation($"--- [SERVER {server.Name} FREE] SimTime: {departureTime:F2}. Triggering dequeue attempt.");
                // Just poke the queue. The queue will handle the rest.
                WaitingLine.TriggerDequeueAttempt(_runContext!);
            };
        }

        // 4. Wire components together
        LoadGenerator.LoadGenerated += HandleLoadGeneratedByGenerator;

        // 5. When a server finishes its work, it must notify the queue that it is free.
        foreach (var server in ServiceChannels)
        {
            server.LoadDeparted += (departedLoad, departureTime) =>
            {
                // The server is now free. Poke the queue to see if it has anyone waiting.
                // The internal logic in the queue will then decide if it should dequeue an item.
                // If it does, the Dequeue event will fire.
                _modelLogger.LogInformation($"--- [SERVER FREE] SimTime: {departureTime:F2}. Server '{server.Name}' pokes queue.");
                WaitingLine.TriggerDequeueAttempt(_runContext!);
            };
        }
    }

    private void HandleLoadGeneratedByGenerator(MyLoad load, double generationTime)
    {
        load.CreationTime = generationTime;
        _modelLogger.LogInformation($"--- [LOAD ARRIVAL] SimTime: {generationTime:F2} -> {load}.");

        // The context is guaranteed to be non-null after Initialize
        var context = _runContext!;

        // Try to send the load directly to an idle server first.
        Server<MyLoad>? idleServer = ServiceChannels.FirstOrDefault(s => s.Vacancy > 0);
        if (idleServer != null)
        {
            _modelLogger.LogInformation($"      Load {load.Id} goes directly to idle Server '{idleServer.Name}'.");
            bool accepted = idleServer.TryStartService(load, context);
            if (accepted)
            {
                TotalLoadsEnteredSystem++;
            }
            else
            {
                // This case is a major logical error if it ever happens
                _modelLogger.LogError($"      CRITICAL ERROR: Idle Server '{idleServer.Name}' rejected load {load.Id}.");
            }
            return; // The load has been handled.
        }

        // If we are here, all servers were busy. Now, try to enqueue.
        _modelLogger.LogInformation($"      All servers busy. Load {load.Id} attempts to enter queue '{WaitingLine.Name}'.");
        bool enqueued = WaitingLine.TryScheduleEnqueue(load, context);
        if (enqueued)
        {
            TotalLoadsEnteredSystem++;
        }
        else
        {
            // The queue is full (or has 0 capacity and is considered full). The load balks.
            BalkedLoadsCount++;
            _modelLogger.LogWarning($"      Load {load.Id} BALKED. Queue '{WaitingLine.Name}' is full. System at capacity K={SystemCapacityK}.");
            // We can fire a "LoadBalked" event from this model if other components need to know.
        }
    }

    public override void Initialize(IRunContext runContext)
    {
        _runContext = runContext;

        LoadGenerator.Initialize(runContext);
        WaitingLine.Initialize(runContext);

        // Setup the action for when an item is dequeued from the WaitingLine
        WaitingLine.LoadDequeued += (dequeuedLoad, dequeueTime) => {
            if (_runContext == null) return;

            // This is the core logic connecting your queue to your servers.
            // It's a perfect example of what an event handler should do.
            Server<MyLoad>? idleServer = ServiceChannels.FirstOrDefault(s => s.Vacancy > 0);
            if (idleServer != null)
            {
                _modelLogger.LogInformation($"      Load {dequeuedLoad.Id} dequeued at {dequeueTime}, Server '{idleServer.Name}' attempting to serve.");
                idleServer.TryStartService(dequeuedLoad, _runContext);
            }
            else
            {
                // This case indicates a logical flaw in the simulation timing or dequeue trigger.
                // A dequeue should only happen if a server slot is expected to be free.
                //
                // This should not happen if DequeueEvent only fires when a server is expected to be free
                // or if this action is the primary way to get an idle server.
                // If all servers are somehow busy again, the item might have to be re-queued or is lost.
                // This indicates a need for robust state checking or a different dequeue trigger.
                _modelLogger.LogWarning($"      CRITICAL: Load {dequeuedLoad.Id} dequeued, but NO idle server found. Re-queuing logic or loss needs consideration.");
            }
        };
    }

    public override void WarmedUp(double simulationTime)
    {
        LoadGenerator.WarmedUp(simulationTime);
        WaitingLine.WarmedUp(simulationTime);
        foreach (var server in ServiceChannels)
        {
            server.WarmedUp(simulationTime);
        }
        foreach (var serverObserver in ServiceChannelObservers)
        {
            serverObserver.WarmedUp(simulationTime);
        }
        BalkedLoadsCount = 0;
        TotalLoadsEnteredSystem = 0;
        _modelLogger.LogInformation("--- M/M/c/K System Warmed Up at {WarmupTime}. Statistics reset. ---", simulationTime);
    }
}