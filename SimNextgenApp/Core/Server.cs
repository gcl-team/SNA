using SimNextgenApp.Configurations;
using SimNextgenApp.Events;
using SimNextgenApp.Exceptions;
using SimNextgenApp.Modeling;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SimNextgenApp.Tests")]
namespace SimNextgenApp.Core;

/// <summary>
/// Represents a server in a DES that processes loads. This component acts as a pure state machine,
/// managing its capacity and processing loads. It emits events via action hooks for external observers to track statistics.
/// </summary>
/// <typeparam name="TLoad">The type of load that this server processes.</typeparam>
public class Server<TLoad> : AbstractSimulationModel, IServer<TLoad>, IOperatableServer<TLoad> where TLoad : notnull
{
    private readonly ServerStaticConfig<TLoad> _config;
    private readonly Random _random;
    private IScheduler? _scheduler;

    internal readonly HashSet<TLoad> _loadsInService;
    internal readonly Dictionary<TLoad, double> _serviceStartTimes;

    /// <inheritdoc/>
    public IReadOnlySet<TLoad> LoadsInService => _loadsInService;

    /// <inheritdoc/>
    public int NumberInService => _loadsInService.Count;

    /// <inheritdoc/>
    public int Capacity => _config.Capacity;

    /// <inheritdoc/>
    public int Vacancy => _config.Capacity - NumberInService;

    /// <summary>
    /// Stores the simulation time at which each load started service.
    /// Key: Load, Value: Start service time (double).
    /// Cleared for a load once it departs and its metrics (like flow time) are potentially recorded.
    /// </summary>
    public IReadOnlyDictionary<TLoad, double> ServiceStartTimes => new ReadOnlyDictionary<TLoad, double>(_serviceStartTimes);

    /// <inheritdoc/>
    public event Action<TLoad, double>? LoadDeparted;

    /// <inheritdoc/>
    public event Action<double>? StateChanged;

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
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (config.Capacity <= 0 && config.Capacity != int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(config), "Server capacity must be positive or int.MaxValue.");

        _random = new Random(seed);
        _loadsInService = [];
        _serviceStartTimes = [];
    }

    /// <inheritdoc/>
    public bool TryStartService(TLoad loadToServe)
    {
        ArgumentNullException.ThrowIfNull(loadToServe);

        if (_scheduler == null)
        {
            throw new InvalidOperationException($"Server '{Name}' has not been initialized with a scheduler.");
        }

        if (Vacancy <= 0)
        {
            return false;
        }

        // 1. Update state
        _loadsInService.Add(loadToServe);
        double currentTime = _scheduler!.ClockTime;
        _serviceStartTimes[loadToServe] = currentTime;

        // 2. Schedule completion
        TimeSpan serviceDuration = _config.ServiceTime(loadToServe, _random);
        _scheduler.Schedule(new ServerServiceCompleteEvent<TLoad>(this, loadToServe), serviceDuration);

        // 3. Notify observers
        OnStateChanged(currentTime);

        return true;
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
        var inServiceLoads = _loadsInService.ToList();
        foreach (var load in inServiceLoads)
        {
            _serviceStartTimes[load] = simulationTime;
        }
    }

    /// <summary>
    /// Handles the completion of service for a specified load at the current server.
    /// </summary>
    /// <remarks>This method removes the specified load from the server's active service list, updates
    /// internal state, and notifies observers of the load's departure and the server's state change.</remarks>
    /// <param name="load">The load for which the service is being completed. Must be currently in service.</param>
    /// <param name="currentTime">The current simulation time at which the service completion occurs.</param>
    /// <exception cref="SimulationException">Thrown if the specified <paramref name="load"/> is not currently in service at the server.</exception>
    internal void HandleServiceCompletion(TLoad load, double currentTime)
    {
        if (!_loadsInService.Remove(load))
        {
            throw new SimulationException($"Attempted to complete service for load '{load}' which was not in service at Server '{Name}'. The simulation state is inconsistent.");
        }

        _serviceStartTimes.Remove(load);

        // Notify observers
        OnLoadDeparted(load, currentTime);
        OnStateChanged(currentTime);
    }

    void IOperatableServer<TLoad>.HandleServiceCompletion(TLoad load, double currentTime)
    {
        HandleServiceCompletion(load, currentTime);
    }

    private void OnLoadDeparted(TLoad load, double time) => LoadDeparted?.Invoke(load, time);
    private void OnStateChanged(double time) => StateChanged?.Invoke(time);
}
