using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Events;
using SimNextgenApp.Modeling;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Core;

/// <summary>
/// Represents a queueing component that holds entities of type <typeparamref name="TLoad"/>
/// according to a specified capacity and (implicitly) a First-In, First-Out (FIFO) discipline.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) managed by this queue.</typeparam>
public class Queue<TLoad> : AbstractSimulationModel
{
    private readonly QueueStaticConfig<TLoad> _config;
    private IScheduler? _scheduler;
    internal ILogger<Queue<TLoad>> Logger { get; }

    /// <summary>
    /// Gets the list of loads currently waiting in the queue.
    /// </summary>
    public List<TLoad> Waiting { get; private set; } = [];

    /// <summary>
    /// Gets the current number of loads (entities) waiting in the queue.
    /// </summary>
    public int Occupancy => Waiting.Count;

    /// <summary>
    /// Gets the configured capacity of the queue.
    /// </summary>
    public int Capacity => _config.Capacity;

    /// <summary>
    /// Gets the number of available spaces remaining in the queue before it reaches its configured capacity.
    /// </summary>
    public int Vacancy => (_config.Capacity == int.MaxValue) ? int.MaxValue : _config.Capacity - Occupancy;

    // Expose configuration if needed, e.g., for events or external checks
    internal QueueStaticConfig<TLoad> Configuration => _config;


    private bool _toDequeue = true;

    /// <summary>
    /// Gets a value indicating whether the queue is currently permitted to dequeue loads.
    /// </summary>
    /// <value>
    /// <c>true</c> if the queue will attempt to dequeue loads when items are available and
    /// dequeue operations are triggered; <c>false</c> otherwise.
    /// </value>
    /// <remarks>
    /// This state can be changed using <see cref="ScheduleUpdateToDequeue"/>.
    /// </remarks>
    public bool ToDequeue => _toDequeue;

    /// <summary>
    /// Gets the time-based metric instance used for tracking queue occupancy statistics over time.
    /// </summary>
    public TimeBasedMetric TimeBasedMetric { get; private set; }

    /// <summary>
    /// Actions to perform when an item is successfully enqueued.
    /// Takes the enqueued load and the simulation time.
    /// </summary>
    public List<Action<TLoad, double>> OnEnqueueActions { get; } = new List<Action<TLoad, double>>();

    /// <summary>
    /// Actions to perform when an item is dequeued.
    /// Takes the dequeued load and the simulation time.
    /// </summary>
    public List<Action<TLoad, double>> OnDequeueActions { get; } = new List<Action<TLoad, double>>();

    /// <summary>
    /// Actions to perform when an item attempts to enqueue but is balked due to full capacity.
    /// Takes the balked load and the simulation time.
    /// </summary>
    public List<Action<TLoad, double>> OnBalkActions { get; } = new List<Action<TLoad, double>>();


    /// <summary>
    /// Actions to perform when the queue's state changes significantly (e.g., occupancy change, ToDequeue status change).
    /// Takes the current simulation time.
    /// </summary>
    public List<Action<double>> OnStateChangeActions { get; } = [];

    /// <summary>
    /// Initialises a new instance of the <see cref="Queue{TLoad}"/> class.
    /// </summary>
    /// <param name="config">The static configuration settings for this queue.</param>
    /// <param name="instanceName">A descriptive name for this queue instance (e.g., "BufferQueue1").</param>
    /// <param name="loggerFactory">The factory used to create loggers for this queue instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> or <paramref name="loggerFactory"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="config.Capacity"/> is not positive and not <see cref="int.MaxValue"/>.</exception>
    public Queue(QueueStaticConfig<TLoad> config, string instanceName, ILoggerFactory loggerFactory)
        : base(instanceName)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (config.Capacity <= 0 && config.Capacity != int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(config), "Queue capacity must be positive or int.MaxValue.");

        _config = config;
        Logger = loggerFactory.CreateLogger<Queue<TLoad>>();
        TimeBasedMetric = new TimeBasedMetric(enableHistory: false); // Or pass initial time from engine if needed
        Logger.LogInformation("Queue '{QueueName}' created with capacity {Capacity}.", Name, _config.Capacity == int.MaxValue ? "Infinite" : _config.Capacity.ToString());
    }

    /// <summary>
    /// Attempts to enqueue the load by scheduling an EnqueueEvent for the current time.
    /// This method is preferred if the enqueue action should be part of the simulation's event chain.
    /// </summary>
    /// <param name="load">The item to enqueue.</param>
    /// <param name="engineContext">The current run context (provides time and scheduler).</param>
    /// <returns>True if an enqueue attempt was scheduled, false if immediate balking occurred (e.g. no scheduler).</returns>
    public bool TryScheduleEnqueue(TLoad load, IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(engineContext);
        EnsureSchedulerInitialized(); // Ensure _scheduler is set via Initialize

        // Pre-check for obvious balking if the queue is finite and definitely full.
        // The EnqueueEvent itself might have more robust logic if state can change between now and event execution.
        if (Vacancy <= 0 && _config.Capacity != int.MaxValue)
        {
            Logger.LogWarning("Queue '{QueueName}' is full. Load {Load} balked immediately at {Time}.", Name, load, engineContext.ClockTime);
            foreach (var action in OnBalkActions) action(load, engineContext.ClockTime);
            return false; // Balk immediately
        }

        engineContext.Scheduler.Schedule(new EnqueueEvent<TLoad>(this, load), engineContext.ClockTime);
        return true;
    }

    public void TriggerDequeueAttempt(IRunContext engineContext)
    {
        EnsureSchedulerInitialized();
        if (this.ToDequeue && this.Occupancy > 0)
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
        TimeBasedMetric.ObserveCount(this.Occupancy, simulationTime);
        Logger.LogInformation("Queue '{QueueName}' warmed up at {Time}. Current occupancy: {Occupancy}", Name, simulationTime, Occupancy);
    }

    private void EnsureSchedulerInitialized()
    {
        if (_scheduler == null)
            throw new InvalidOperationException($"Queue '{Name}' has not been initialized with a scheduler. Call Initialize first.");
    }

    // Internal method for UpdateToDequeueEvent to call, keeping public ToDequeue readonly
    internal void SetToDequeueState(bool newState)
    {
        _toDequeue = newState;
    }
}