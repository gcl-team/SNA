using SimNextgenApp.Configurations;
using SimNextgenApp.Modeling;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Core
{
    /// <summary>
    /// Represents a server in a discrete event simulation that processes loads.
    /// It can hold a certain number of loads simultaneously (capacity) and takes
    /// a defined service time to process each load.
    /// </summary>
    /// <typeparam name="TLoad">The type of load that this server processes.</typeparam>
    public class Server<TLoad> : AbstractSimulationModel
    {
        private readonly ServerStaticConfig<TLoad> _config;
        private readonly Random _random;
        private IScheduler? _scheduler;

        /// <summary>
        /// Gets the set of loads currently being processed (in service) by the server.
        /// </summary>
        public HashSet<TLoad> LoadsInService { get; }

        /// <summary>
        /// Gets the number of loads currently being processed by the server.
        /// </summary>
        public int NumberInService => LoadsInService.Count;

        /// <summary>
        /// Gets the number of available slots for new loads, based on the server's capacity.
        /// </summary>
        public int Vacancy => _config.Capacity - NumberInService;

        // Statistics
        /// <summary>
        /// Tracks the number of busy server units over time.
        /// Used to calculate utilization.
        /// </summary>
        public TimeBasedMetric BusyServerUnitsCounter { get; }

        /// <summary>
        /// Gets the total number of loads that have completed service.
        /// </summary>
        public int LoadsCompletedCount { get; private set; }

        /// <summary>
        /// Gets the average server utilization (number of busy units / capacity).
        /// Requires <see cref="TimeBasedMetric.SetTimeFrame"/> to be called on <see cref="BusyServerUnitsCounter"/>
        /// after the simulation run (or relevant period) to calculate averages.
        /// </summary>
        public double Utilization => _config.Capacity > 0 ? BusyServerUnitsCounter.AverageCount / _config.Capacity : 0.0;

        /// <summary>
        /// Stores the simulation time at which each load started service.
        /// Key: Load, Value: Start service time (double).
        /// Cleared for a load once it departs and its metrics (like flow time) are potentially recorded.
        /// </summary>
        public Dictionary<TLoad, double> ServiceStartTimes { get; }

        // Event Hooks
        /// <summary>
        /// Actions to execute when a load departs from the server after completing service.
        /// Takes the departed load and its service completion time.
        /// </summary>
        public List<Action<TLoad, double>> LoadDepartActions { get; }

        /// <summary>
        /// Actions to execute when the server's state changes (e.g., becomes busy, becomes idle, load departs).
        /// Takes the current simulation time.
        /// </summary>
        public List<Action<double>> StateChangeActions { get; }


        /// <summary>
        /// Initializes a new instance of the <see cref="Server{TLoad}"/> class.
        /// </summary>
        /// <param name="config">The static configuration for this server.</param>
        /// <param name="seed">The seed for the random number stream used by this server.</param>
        /// <param name="instanceName">A unique name for this server instance (e.g., "CheckoutCounter1").</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if essential configuration like ServiceTime is missing.</exception>
        public Server(ServerStaticConfig<TLoad> config, int seed, string instanceName)
            : base(instanceName)
        {
            ArgumentNullException.ThrowIfNull(config);
            
            if (config.Capacity <= 0 && config.Capacity != int.MaxValue) // Allow int.MaxValue for "infinite"
                throw new ArgumentOutOfRangeException(nameof(config), "Server capacity must be positive or int.MaxValue.");

            _config = config;
            _random = new Random(seed);

            LoadsInService = new HashSet<TLoad>();
            ServiceStartTimes = new Dictionary<TLoad, double>();

            BusyServerUnitsCounter = new TimeBasedMetric();
            LoadsCompletedCount = 0;

            LoadDepartActions = new List<Action<TLoad, double>>();
            StateChangeActions = new List<Action<double>>();
        }

        /// <summary>
        /// Attempts to start serving the given load if capacity is available.
        /// Schedules a <see cref="ServerStartServiceEvent{TLoad}"/> for the current simulation time.
        /// </summary>
        /// <param name="engine">The simulation engine instance.</param>
        /// <param name="loadToServe">The load to start serving.</param>
        /// <returns><c>true</c> if the load could be accepted (i.e., vacancy > 0 and event scheduled); <c>false</c> otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown if Initialize has not been called yet.</exception>
        /// <exception cref="ArgumentNullException">Thrown if engine or loadToServe is null.</exception>
        public bool TryStartService(IRunContext engine, TLoad loadToServe)
        {
            ArgumentNullException.ThrowIfNull(engine);
            ArgumentNullException.ThrowIfNull(loadToServe);
            EnsureSchedulerInitialized();

            if (Vacancy > 0)
            {
                _scheduler!.Schedule(new ServerStartServiceEvent<TLoad>(this, loadToServe), engine.ClockTime);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override void Initialize(IScheduler scheduler)
        {
            ArgumentNullException.ThrowIfNull(scheduler);
            _scheduler = scheduler;
        }

        /// <inheritdoc/>
        public override void WarmedUp(double simulationTime)
        {
            // Reset statistics at the end of warm-up
            BusyServerUnitsCounter.WarmedUp(simulationTime);
            LoadsCompletedCount = 0;
            // Loads currently in service might continue, or be cleared depending on model logic.
            // For simplicity, let's assume they continue, and their stats before warmup are ignored by WarmedUp.
            // ServiceStartTimes for ongoing loads might need adjustment if flow time stats are critical across warmup.
            if (LoadsInService.Count > 0)
            {
                BusyServerUnitsCounter.ObserveCount(LoadsInService.Count, simulationTime);
            }
        }

        private void EnsureSchedulerInitialized()
        {
            if (_scheduler == null)
                throw new InvalidOperationException($"Server '{Name}' has not been initialized with a scheduler.");
        }

        internal void PerformLoadCompletedCountUpdate(int loadCompletedCount)
        {
            LoadsCompletedCount = loadCompletedCount;
        }

        internal void HandleLoadArrivalForService(TLoad load, double currentTime)
        {
            if (Vacancy <= 0)
            {
                // This should ideally be caught by TryStartService or queueing logic before this point.
                // If it happens, it's an unexpected state, potentially throw.
                // For now, let's assume TryStartService prevents this.
                // Or, if a queue feeds this server, the queue handles full capacity.
                // If no queue, and an attempt is made to push to a full server, it's a model design issue.
                throw new InvalidOperationException($"Server '{Name}' cannot start service for load. No vacancy. This should be handled by upstream logic.");
            }

            LoadsInService.Add(load);
            ServiceStartTimes[load] = currentTime;

            BusyServerUnitsCounter.ObserveChange(1, currentTime);
            InvokeStateChangeActions(currentTime);

            TimeSpan serviceDuration = _config.ServiceTime(load, _random);
            _scheduler!.Schedule(
                new ServerServiceCompleteEvent<TLoad>(this, load),
                currentTime + serviceDuration.TotalSeconds // Adjust TotalSeconds if clock units differ
            );
        }

        internal void HandleServiceCompletion(TLoad load, double currentTime)
        {
            if (!LoadsInService.Remove(load))
            {
                // Load was not in service, which is unexpected.
                // Log warning or throw, depending on strictness.
                // For now, proceed assuming it was removed.
            }

            ServiceStartTimes.Remove(load); // Clean up start time record
            LoadsCompletedCount++;
            BusyServerUnitsCounter.ObserveChange(-1, currentTime);

            // Invoke departure actions
            foreach (var action in LoadDepartActions)
            {
                action(load, currentTime);
            }
            InvokeStateChangeActions(currentTime);
        }

        private void InvokeStateChangeActions(double currentTime)
        {
            foreach (var action in StateChangeActions)
            {
                action(currentTime);
            }
        }
    }

    internal abstract class AbstractServerEvent<TLoad> : AbstractEvent
    {
        internal Server<TLoad> OwningServer { get; }

        protected AbstractServerEvent(Server<TLoad> owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            OwningServer = owner;
        }
    }

    /// <summary>
    /// Event representing a load starting service at the server.
    /// </summary>
    internal sealed class ServerStartServiceEvent<TLoad> : AbstractServerEvent<TLoad>
    {
        public TLoad LoadToServe { get; }

        public ServerStartServiceEvent(Server<TLoad> owner, TLoad loadToServe) : base(owner)
        {
            ArgumentNullException.ThrowIfNull(loadToServe);
            LoadToServe = loadToServe;
        }

        public override void Execute(IRunContext engine)
        {
            OwningServer.HandleLoadArrivalForService(LoadToServe, engine.ClockTime);
        }

        public override string ToString() => $"{OwningServer.Name}_StartService({LoadToServe})#{EventId} @ {ExecutionTime:F4}";
    }

    /// <summary>
    /// Event representing a load completing service at the server.
    /// </summary>
    internal sealed class ServerServiceCompleteEvent<TLoad> : AbstractServerEvent<TLoad>
    {
        public TLoad ServedLoad { get; }

        public ServerServiceCompleteEvent(Server<TLoad> owner, TLoad servedLoad) : base(owner)
        {
            ArgumentNullException.ThrowIfNull(servedLoad);
            ServedLoad = servedLoad;
        }

        public override void Execute(IRunContext engine)
        {
            OwningServer.HandleServiceCompletion(ServedLoad, engine.ClockTime);
        }

        public override string ToString() => $"{OwningServer.Name}_ServiceComplete({ServedLoad})#{EventId} @ {ExecutionTime:F4}";
    }
}