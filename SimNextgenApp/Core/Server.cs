using SimNextgenApp.Configurations;
using SimNextgenApp.Events;
using SimNextgenApp.Exceptions;
using SimNextgenApp.Modeling;

namespace SimNextgenApp.Core;

/// <summary>
/// Represents a server in a DES that processes loads. This component acts as a pure state machine,
/// managing its capacity and processing loads. It emits events via action hooks for external observers to track statistics.
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
    /// Gets the configured capacity of the server.
    /// </summary>
    public int Capacity => _config.Capacity;

    /// <summary>
    /// Gets the number of available slots for new loads, based on the server's capacity.
    /// </summary>
    public int Vacancy => _config.Capacity - NumberInService;

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
    /// Initialises a new instance of the <see cref="Server{TLoad}"/> class.
    /// </summary>
    /// <param name="config">The static configuration for this server.</param>
    /// <param name="seed">The seed for the random number stream used by this server.</param>
    /// <param name="instanceName">A unique name for this server instance (e.g., "CheckoutCounter1").</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if essential configuration like ServiceTime is missing.</exception>
    public Server(ServerStaticConfig<TLoad> config, int seed, string instanceName) : base(instanceName)
    {
        ArgumentNullException.ThrowIfNull(config);
        
        if (config.Capacity <= 0 && config.Capacity != int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(config), "Server capacity must be positive or int.MaxValue.");

        _config = config;
        _random = new Random(seed);

        LoadsInService = [];
        ServiceStartTimes = [];

        LoadDepartActions = [];
        StateChangeActions = [];
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
        // For any loads currently being served, reset their start time
        // so that their flow time (Time of Departure - Time of Arrival) is measured from this point forward.
        var inServiceLoads = LoadsInService.ToList();
        foreach (var load in inServiceLoads)
        {
            ServiceStartTimes[load] = simulationTime;
        }
    }

    private void EnsureSchedulerInitialized()
    {
        if (_scheduler == null)
            throw new InvalidOperationException($"Server '{Name}' has not been initialized with a scheduler.");
    }

    internal void HandleLoadArrivalForService(TLoad load, double currentTime)
    {
        if (Vacancy <= 0)
        {
            throw new InvalidOperationException($"Server '{Name}' cannot start service for load. No vacancy. This should be handled by upstream logic.");
        }

        LoadsInService.Add(load);
        ServiceStartTimes[load] = currentTime;

        TimeSpan serviceDuration = _config.ServiceTime(load, _random);
        _scheduler!.Schedule(new ServerServiceCompleteEvent<TLoad>(this, load), serviceDuration);

        foreach (var action in StateChangeActions)
        {
            action(currentTime);
        }
    }

    internal void HandleServiceCompletion(TLoad load, double currentTime)
    {
        if (!LoadsInService.Remove(load))
        {
            throw new SimulationException($"Attempted to complete service for load '{load}' which was not in service at Server '{Name}'. The simulation state is inconsistent.");
        }

        ServiceStartTimes.Remove(load);

        foreach (var action in LoadDepartActions)
        {
            action(load, currentTime);
        }

        foreach (var action in StateChangeActions)
        {
            action(currentTime);
        }
    }
}
