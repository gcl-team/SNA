using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Modeling;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Demo.CustomModels;

internal class SimpleMmckModel : AbstractSimulationModel
{
    public Generator<MyLoad> LoadGenerator { get; }
    public SimQueue<MyLoad> WaitingLine { get; }
    public List<Server<MyLoad>> ServiceChannels { get; }
    public List<ServerObserver<MyLoad>> ServiceChannelObservers { get; }

    private readonly ILogger<SimpleMmckModel> _modelLogger;
    private IRunContext? _runContext;

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
                _modelLogger.LogInformation($"--- [SERVER {server.Name} FREE] SimTime: {departureTime:F2} after serving {departedLoad}.");
                if (_runContext != null && WaitingLine.ToDequeue && WaitingLine.Occupancy > 0)
                {
                    _modelLogger.LogInformation($"      Server '{server.Name}' is free, queue has items. Triggering dequeue attempt.");
                    WaitingLine.TriggerDequeueAttempt(_runContext);
                }
            };
        }

        // --- Wire components together ---

        // A. When a load is generated:
        LoadGenerator.LoadGeneratedActions.Add(HandleLoadGeneratedByGenerator);

        // B. When a load is dequeued from the queue (if queue manages pushing to server):
        // WaitingLine.OnDequeueActions.Add(HandleLoadDequeuedFromWaitingLine);
        // OR (more common) C. When a server becomes free, it tries to pull from the queue:
        foreach (var server in ServiceChannels)
        {
            server.LoadDeparted += (departedLoad, departureTime) =>
            {
                // server has just become free (or one of its units has)
                TryServeNextFromQueue(server, departureTime); // Pass server that just finished
            };
        }
    }

    private void HandleLoadGeneratedByGenerator(MyLoad load, double generationTime)
    {
        load.CreationTime = generationTime;
        _modelLogger.LogInformation($"--- [LOAD ARRIVAL] SimTime: {generationTime:F2} -> {load}.");

        if (_runContext == null)
        {
            _modelLogger.LogError("RunContext not available. Model not properly initialized.");
            return;
        }

        // Check K: System Capacity
        int currentTotalInSystem = WaitingLine.Occupancy;
        foreach (var server in ServiceChannels)
        {
            currentTotalInSystem += server.NumberInService;
        }

        WaitingLine.TryScheduleEnqueue(load, _runContext);

        WaitingLine.LoadBalked += (balkedLoad, balkTime) => {
            BalkedLoadsCount++;
            _modelLogger.LogInformation($"      M/M/c/K model noted that Load {balkedLoad.Id} balked at time {balkTime}.");
        };

        TotalLoadsEnteredSystem++;
        _modelLogger.LogTrace($"      Load {load.Id} enters system. Total in system before this load: {currentTotalInSystem - WaitingLine.Occupancy - ServiceChannels.Sum(s => s.NumberInService)} (Pre-check) -> now {currentTotalInSystem} (Post-check, pre-placement).");

        // Try to send to an idle server directly
        Server<MyLoad>? idleServer = ServiceChannels.FirstOrDefault(s => s.Vacancy > 0);
        if (idleServer != null)
        {
            _modelLogger.LogInformation($"      Load {load.Id} goes directly to idle Server '{idleServer.Name}'.");
            bool accepted = idleServer.TryStartService(load);
            if (!accepted)
            {
                // This should not happen if Vacancy > 0 was true unless there's a race or an issue
                _modelLogger.LogError($"      ERROR: Idle Server '{idleServer.Name}' failed to accept load {load.Id}. This is unexpected.");
                // Potentially try to queue it now, or treat as an error/balk
                TryEnqueueLoad(load, generationTime, " (after server direct fail)");
            }
        }
        else
        {
            // All servers busy, try to enqueue
            _modelLogger.LogInformation($"      All servers busy. Load {load.Id} attempts to enter queue '{WaitingLine.Name}'.");
            TryEnqueueLoad(load, generationTime);
        }
    }

    private void TryEnqueueLoad(MyLoad load, double time, string contextSuffix = "")
    {
        if (_runContext == null) return;

        // The queue's capacity (K-c) is already set. TryScheduleEnqueue will use it.
        bool enqueued = WaitingLine.TryScheduleEnqueue(load, _runContext);
        if (enqueued)
        {
            _modelLogger.LogInformation($"      Load {load.Id} enqueued in '{WaitingLine.Name}'.{contextSuffix}");
        }
        else
        {
            // This means the queue itself (waiting spots K-c) was full.
            // This case should have been caught by the (currentTotalInSystem >= SystemCapacityK) check earlier.
            // If it happens here, it's likely a logic discrepancy or a very tight race if K was exactly met by queueing.
            BalkedLoadsCount++; // Increment again or ensure balk is only counted once
            _modelLogger.LogWarning($"      Load {load.Id} BALKED from Queue '{WaitingLine.Name}'.{contextSuffix} Queue full. This might indicate K was met by queueing.");
        }
    }


    // Called when a server finishes a load and might be able to take one from the queue
    private void TryServeNextFromQueue(Server<MyLoad> newlyFreedServer, double currentTime)
    {
        if (_runContext == null)
        {
            _modelLogger.LogError("RunContext not available in TryServeNextFromQueue.");
            return;
        }

        if (WaitingLine.Occupancy > 0)
        {
            if (newlyFreedServer.Vacancy > 0) // Ensure this specific server unit is free
            {
                _modelLogger.LogInformation($"      Server '{newlyFreedServer.Name}' is free. Attempting to pull from queue '{WaitingLine.Name}'.");
                // To dequeue, we need to schedule a DequeueEvent or have Queue manage it.
                // For now, let's assume Queue's OnDequeueActions list is empty and we manage it here.
                // This requires a method on Queue that directly dequeues and returns the item,
                // OR scheduling a DequeueEvent and then having its OnDequeueAction trigger service.

                // Let's refine: DequeueEvent in Queue should have OnDequeueActions.
                // One of those actions will be to offer it to an idle server.
                // This requires careful coordination.

                // Simpler approach for now: If server is free, and queue has items,
                // the server itself tries to pull by scheduling a dequeue *for itself*.
                // This needs a bit more thought on who "owns" the dequeue trigger.

                // Let's assume a slightly different model: when a server becomes free,
                // it simply *signals* that it's free. A central dispatcher or the queue itself
                // then decides what to do.

                // For this M/M/c/K, a common pattern:
                // Server becomes free. If queue is not empty, schedule an event for *this server*
                // to *attempt to take from queue*. This event would then call a method on queue like
                // `TryDequeueAndServe(serverInstance, context)`
                // OR, more simply for now, let the queue's auto-dequeue logic (if enabled and items exist) handle it.

                // If the queue has `ToDequeue = true` and items, its internal `DequeueEvent` will fire.
                // That `DequeueEvent`'s `OnDequeueActions` needs to find an idle server.
                // Let's add an action to WaitingLine.OnDequeueActions:
                // (This is done in the constructor now)
            }
        }
    }


    public override void Initialize(IScheduler scheduler)
    {
        if (scheduler is IRunContext context)
        {
            _runContext = context;
        }
        else
        {
            throw new InvalidOperationException("MMCKSystemModel requires IRunContext for initialization.");
        }

        LoadGenerator.Initialize(scheduler);
        WaitingLine.Initialize(scheduler);
        foreach (var server in ServiceChannels)
        {
            server.Initialize(scheduler);
        }

        // Setup the action for when an item is dequeued from the WaitingLine
        WaitingLine.LoadDequeued += (dequeuedLoad, dequeueTime) => {
            if (_runContext == null) return;

            // This is the core logic connecting your queue to your servers.
            // It's a perfect example of what an event handler should do.
            Server<MyLoad>? idleServer = ServiceChannels.FirstOrDefault(s => s.Vacancy > 0);
            if (idleServer != null)
            {
                _modelLogger.LogInformation($"      Load {dequeuedLoad.Id} dequeued at {dequeueTime}, Server '{idleServer.Name}' attempting to serve.");
                idleServer.TryStartService(dequeuedLoad);
            }
            else
            {
                // This should not happen if DequeueEvent only fires when a server is expected to be free
                // or if this action is the primary way to get an idle server.
                // If all servers are somehow busy again, the item might have to be re-queued or is lost.
                // This indicates a need for robust state checking or a different dequeue trigger.
                _modelLogger.LogWarning($"      Load {dequeuedLoad.Id} dequeued, but NO idle server found. Re-queuing logic or loss needs consideration.");
                // For now, assume it might get picked up if another server frees up and polls, or re-enqueue if possible.
                // Re-queuing immediately could cause a loop if not careful.
                // A better model might be that the DequeueEvent checks for an idle server *before* fully dequeuing.
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

// Helper extension for invoking actions (optional)
public static class ActionListExtensions
{
    public static void InvokeForAll<T1, T2>(this List<Action<T1, T2>> actions, Action<Action<T1, T2>> invoker)
    {
        foreach (var action in actions.ToList()) // ToList for safe iteration if action modifies list
        {
            invoker(action);
        }
    }
}