using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Modeling;

/// <summary>
/// Represents a queueing component that holds entities of type <typeparamref name="TLoad"/>
/// based on a FIFO discipline and a specified capacity.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) managed by this queue.</typeparam>
public class SimQueue<TLoad> : AbstractSimulationModel, ISimQueue<TLoad>, IOperatableQueue<TLoad>
    where TLoad : notnull
{
    private readonly QueueStaticConfig<TLoad> _config;
    private IScheduler? _scheduler;
    private readonly Queue<TLoad> _waitingItems = new();
    private bool _toDequeue = true;

    public event Action<TLoad, double>? LoadEnqueued;
    public event Action<TLoad, double>? LoadDequeued;
    public event Action<TLoad, double>? LoadBalked;
    public event Action<double>? StateChanged;

    public IReadOnlyCollection<TLoad> WaitingItems => _waitingItems;
    public int Occupancy => _waitingItems.Count;
    public int Capacity => _config.Capacity;
    public int Vacancy => _config.Capacity == int.MaxValue ? int.MaxValue : _config.Capacity - Occupancy;
    public bool ToDequeue => _toDequeue;

    /// <summary>
    /// Gets the time-based metric instance used for tracking queue occupancy statistics over time.
    /// </summary>
    public TimeBasedMetric TimeBasedMetric { get; private set; }

    internal ILogger<SimQueue<TLoad>> Logger { get; }

    /// <summary>
    /// Gets the configuration settings for the queue.
    /// </summary>
    /// <remarks>Expose configuration if needed, e.g., for events or external checks</remarks>
    internal QueueStaticConfig<TLoad> Configuration => _config;

    /// <summary>
    /// Initialises a new instance of the <see cref="SimQueue{TLoad}"/> class.
    /// </summary>
    /// <param name="config">The static configuration settings for this queue.</param>
    /// <param name="instanceName">A descriptive name for this queue instance (e.g., "BufferQueue1").</param>
    /// <param name="loggerFactory">The factory used to create loggers for this queue instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> or <paramref name="loggerFactory"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="config.Capacity"/> is not positive and not <see cref="int.MaxValue"/>.</exception>
    public SimQueue(QueueStaticConfig<TLoad> config, string instanceName, ILoggerFactory loggerFactory)
        : base(instanceName)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (config.Capacity <= 0 && config.Capacity != int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(config), "Queue capacity must be positive or int.MaxValue.");

        _config = config;
        Logger = loggerFactory.CreateLogger<SimQueue<TLoad>>();
        TimeBasedMetric = new TimeBasedMetric(enableHistory: false); // Or pass initial time from engine if needed
        Logger.LogInformation("Queue '{QueueName}' created with capacity {Capacity}.", Name, _config.Capacity == int.MaxValue ? "Infinite" : _config.Capacity.ToString());
    }

    /// <summary>
    /// Attempts to enqueue the load by scheduling an EnqueueEvent for the current time.
    /// This method is preferred if the enqueue action should be part of the simulation's event chain.
    /// </summary>
    /// <param name="load">The item to enqueue.</param>
    /// <param name="engineContext">The current run context (provides time and scheduler).</param>
    /// <returns>
    /// <c>true</c> if an enqueue event was successfully scheduled; 
    /// <c>false</c> if the queue is already at full capacity and the load balked.
    /// </returns>
    public bool TryScheduleEnqueue(TLoad load, IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(engineContext);
        EnsureSchedulerInitialized();

       if (Vacancy <= 0 && _config.Capacity != int.MaxValue)
        {
            Logger.LogWarning("Attempted to enqueue {Load} into full queue {QueueName} at {Time}. BALKING.",
                load, Name, engineContext.ClockTime);

            OnLoadBalked(load, engineContext.ClockTime);

            return false; // Balk immediately - Do not enqueue
        }

        engineContext.Scheduler.Schedule(new EnqueueEvent<TLoad>(this, load), engineContext.ClockTime);
        return true;
    }

    /// <summary>
    /// Triggers an attempt to dequeue an item from the queue if certain conditions are met.
    /// </summary>
    /// <remarks>This method schedules a dequeue event if the queue is marked for dequeuing (<see  cref="ToDequeue"/> is <see langword="true"/>) 
    /// and the queue has at least one item.
    /// The event is scheduled using the provided execution context's scheduler at the current clock time.</remarks>
    /// <param name="engineContext">The context of the current execution, providing access to the scheduler and clock time.</param>
    public void TriggerDequeueAttempt(IRunContext engineContext)
    {
        EnsureSchedulerInitialized();
        if (ToDequeue && Occupancy > 0)
        {
            Logger.LogTrace("Dequeue attempt triggered for {QueueName} at {Time} by external signal.", Name, engineContext.ClockTime);
            engineContext.Scheduler.Schedule(new DequeueEvent<TLoad>(this), engineContext.ClockTime);
        }
    }

    /// <summary>
    /// Schedules an event to update the ToDequeue status of the queue.
    /// </summary>
    /// <param name="toDequeue">The new dequeue status.</param>
    /// <param name="engineContext">The current run context.</param>
    public void ScheduleUpdateToDequeue(bool toDequeue, IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(engineContext);
        EnsureSchedulerInitialized();
        engineContext.Scheduler.Schedule(new UpdateToDequeueEvent<TLoad>(this, toDequeue), engineContext.ClockTime);
    }

    /// <summary>
    /// Initialises the queue with the specified scheduler.
    /// </summary>
    /// <param name="scheduler">The scheduler to be used for managing queue operations. Cannot be <see langword="null"/>.</param>
    public override void Initialize(IScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
        TimeBasedMetric.ObserveCount(0, 0); // Initialize metric at time 0 with 0 count
        Logger.LogInformation("Queue '{QueueName}' initialized.", Name);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Resets statistics for the post-warm-up period. The queue's <see cref="TimeBasedMetric"/>
    /// is reset, and its current occupancy is observed at the <paramref name="simulationTime"/>
    /// to establish a baseline for post-warm-up statistics.
    /// </remarks>
    public override void WarmedUp(double simulationTime)
    {
        TimeBasedMetric.WarmedUp(simulationTime);
        // After metric is warmed up (its count is 0, time is simulationTime),
        // observe the actual queue length at this moment.
        TimeBasedMetric.ObserveCount(Occupancy, simulationTime);
        Logger.LogInformation("Queue '{QueueName}' warmed up at {Time}. Current occupancy: {Occupancy}", Name, simulationTime, Occupancy);
    }

    /// <summary>
    /// Handles the enqueue operation for the queue at the specified time.
    /// </summary>
    /// <param name="load">The load that will be enqueued.</param>
    /// <param name="currentTime">The current time at which the enqueue operation is being processed.</param>
    internal void HandleEnqueue(TLoad load, double currentTime)
    {
        if (Vacancy <= 0 && Configuration.Capacity != int.MaxValue)
        {
            Logger.LogError("EnqueueEvent for {QueueName} (Capacity: {Capacity}) found queue full upon execution. Load {Load} will be dropped.",
                Name, Configuration.Capacity, load);
            return;
        }

        _waitingItems.Enqueue(load);
        TimeBasedMetric.ObserveChange(Occupancy, currentTime);
        Logger.LogTrace("Enqueued {Load} into {QueueName} at {Time}. New Occupancy: {Occupancy}",
            load, Name, currentTime, Occupancy);

        // Notify observers
        OnLoadEnqueued(load,currentTime);
        OnStateChanged(currentTime);
    }

    /// <summary>
    /// Handles the dequeue operation for the queue at the specified time.
    /// </summary>
    /// <param name="currentTime">The current time at which the dequeue operation is being processed.</param>
    internal void HandleDequeue(double currentTime)
    {
        if (Occupancy == 0)
        {
            Logger.LogTrace("DequeueEvent for {QueueName}: Queue is empty. No action.", Name);
            return;
        }

        if (!ToDequeue)
        {
            Logger.LogTrace("DequeueEvent for {QueueName}: Queue is not set to ToDequeue. No action.", Name);
            return;
        }

        var load = _waitingItems.Dequeue();
        TimeBasedMetric.ObserveChange(Occupancy, currentTime);
        Logger.LogTrace("Dequeued {Load} from {QueueName} at {Time}. New Occupancy: {Occupancy}",
            load, Name, currentTime, Occupancy);

        // Notify observers
        OnLoadDequeued(load, currentTime);
        OnStateChanged(currentTime);
    }

    /// <summary>
    /// Updates the dequeue permission of the queue, ToDequeue, and notifies observers if the state changes.
    /// </summary>
    /// <remarks>If the new state is the same as the current state, no action is taken.</remarks>
    /// <param name="toDequeue">A value indicating whether the queue should be allowed for dequeue.</param>
    /// <param name="currentTime">The current time, used to log and notify observers of the state change.</param>
    internal void HandleUpdateToDequeue(bool toDequeue, double currentTime)
    {
        if (_toDequeue == toDequeue) return;

        Logger.LogTrace("UpdateToDequeueEvent for {QueueName}: ToDequeue changed from {OldState} to {NewState} at {Time}",
            Name, _toDequeue, toDequeue, currentTime);
        
        _toDequeue = toDequeue;

        // Notify observers
        OnStateChanged(currentTime);
    }

    void IOperatableQueue<TLoad>.HandleEnqueue(TLoad load, double currentTime) => HandleEnqueue(load, currentTime);
    void IOperatableQueue<TLoad>.HandleDequeue(double currentTime) => HandleDequeue(currentTime);
    void IOperatableQueue<TLoad>.HandleUpdateToDequeue(bool toDequeue, double currentTime) => HandleUpdateToDequeue(toDequeue, currentTime);

    private void EnsureSchedulerInitialized()
    {
        if (_scheduler == null)
            throw new InvalidOperationException($"Queue '{Name}' has not been initialized with a scheduler. Call Initialize first.");
    }

    private void OnLoadEnqueued(TLoad load, double time) => LoadEnqueued?.Invoke(load, time);
    private void OnLoadDequeued(TLoad load, double time) => LoadDequeued?.Invoke(load, time);
    private void OnLoadBalked(TLoad load, double time) => LoadBalked?.Invoke(load, time);
    private void OnStateChanged(double time) => StateChanged?.Invoke(time);
}