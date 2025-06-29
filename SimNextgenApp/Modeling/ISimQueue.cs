using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling;

public interface ISimQueue<TLoad> : IWarmupAware
{
    /// <summary>
    /// Gets the unique identifier assigned to this simulation model instance.
    /// Typically assigned during instantiation.
    /// </summary>
    long Id { get; }

    /// <summary>
    /// Gets a descriptive, human-readable name for this simulation model instance.
    /// Useful for logging and results reporting. Typically set during instantiation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the queue is currently permitted to dequeue loads.
    /// </summary>
    /// <value>
    /// <c>true</c> if the queue will attempt to dequeue loads when items are available and
    /// dequeue operations are triggered; <c>false</c> otherwise.
    /// </value>
    /// <remarks>
    /// This state can be changed using ScheduleUpdateToDequeue.
    /// </remarks>
    bool ToDequeue { get; }

    /// <summary>
    /// Gets the current number of loads (entities) waiting in the queue.
    /// </summary>
    /// <value>The number of items currently in the queue.</value>
    int Occupancy { get; }

    /// <summary>
    /// Gets the configured capacity of the queue.
    /// </summary>
    /// <value>The capacity of the queue.</value>
    int Capacity { get; }

    /// <summary>
    /// Gets the number of available spaces remaining in the queue before it reaches its configured capacity.
    /// </summary>
    /// <value>The number of available spaces remaining in the queue.</value>
    int Vacancy { get; }

    /// <summary>
    /// Gets a read-only collection of loads currently waiting in the queue.
    /// </summary>
    IReadOnlyCollection<TLoad> WaitingItems {  get; }

    /// <summary>
    /// Occurs when a load has been successfully enqueued.
    /// </summary>
    event Action<TLoad, double>? LoadEnqueued;

    /// <summary>
    /// Occurs when a load has been successfully dequeued.
    /// </summary>
    event Action<TLoad, double>? LoadDequeued;

    /// <summary>
    /// Occurs when an item attempts to enqueue but is balked due to a full queue.
    /// </summary>
    event Action<TLoad, double>? LoadBalked;

    /// <summary>
    /// Occurs when the queue state changes, such as occupancy or its ToDequeue status.
    /// </summary>
    event Action<double>? StateChanged;

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
    bool TryScheduleEnqueue(TLoad load, IRunContext engineContext);

    /// <summary>
    /// Triggers an attempt to dequeue an item from the queue if certain conditions are met.
    /// </summary>
    /// <remarks>This method schedules a dequeue event if the queue is marked for dequeuing (<see  cref="ToDequeue"/> is <see langword="true"/>) 
    /// and the queue has at least one item.
    /// The event is scheduled using the provided execution context's scheduler at the current clock time.</remarks>
    /// <param name="engineContext">The context of the current execution, providing access to the scheduler and clock time.</param>
    void TriggerDequeueAttempt(IRunContext engineContext);

    /// <summary>
    /// Schedules an event to update the ToDequeue status of the queue.
    /// </summary>
    /// <param name="toDequeue">The new dequeue status.</param>
    /// <param name="engineContext">The current run context.</param>
    void ScheduleUpdateToDequeue(bool toDequeue, IRunContext engineContext);
}
