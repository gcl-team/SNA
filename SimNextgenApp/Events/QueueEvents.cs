using Microsoft.Extensions.Logging;
using SimNextgenApp.Core;

namespace SimNextgenApp.Events;

/// <summary>
/// Base class for events specific to the internal operations of a <see cref="Core.Queue{TLoad}"/>.
/// </summary>
/// <typeparam name="TLoad">The type of load managed by the queue.</typeparam>
internal abstract class AbstractQueueEvent<TLoad> : AbstractEvent
{
    /// <summary>
    /// Gets the queue instance that this event pertains to.
    /// </summary>
    internal Core.Queue<TLoad> OwningQueue { get; }


    /// <inheritdoc/>
    public override IDictionary<string, object>? GetTraceDetails()
    {
        return new Dictionary<string, object>
        {
            { "GeneratorName", OwningQueue.Name },
            { "Vacancy", OwningQueue.Vacancy },
            { "Waiting", OwningQueue.Waiting },
            { "Capacity", OwningQueue.Capacity }
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractQueueEvent{TLoad}"/> class.
    /// </summary>
    /// <param name="owner">The queue that owns this event.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="owner"/> is null.</exception>
    protected AbstractQueueEvent(Core.Queue<TLoad> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        OwningQueue = owner;
    }
}

/// <summary>
/// Event that handles adding a <typeparamref name="TLoad"/> entity to the <see cref="AbstractQueueEvent{TLoad}.OwningQueue"/>.
/// </summary>
/// <typeparam name="TLoad">The type of load being enqueued.</typeparam>
internal sealed class EnqueueEvent<TLoad> : AbstractQueueEvent<TLoad>
{
    /// <summary>
    /// Gets the load entity to be added to the queue.
    /// </summary>
    internal TLoad LoadToEnqueue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueEvent{TLoad}"/> class.
    /// </summary>
    /// <param name="owner">The queue that will receive the load.</param>
    /// <param name="loadToEnqueue">The load to be enqueued.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="owner"/> or <paramref name="loadToEnqueue"/> is null.</exception>
    public EnqueueEvent(Core.Queue<TLoad> owner, TLoad loadToEnqueue) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(loadToEnqueue, nameof(loadToEnqueue));
        LoadToEnqueue = loadToEnqueue;
    }

    /// <summary>
    /// Adds the load to the queue, updates statistics, and may trigger a dequeue attempt.
    /// </summary>
    /// <param name="engine">The simulation run context.</param>
    public override void Execute(IRunContext engine)
    {
        // Safeguard: Check if the queue is unexpectedly full for finite queues.
        if (OwningQueue.Vacancy <= 0 && OwningQueue.Configuration.Capacity != int.MaxValue)
        {
            OwningQueue.Logger.LogError("EnqueueEvent for {QueueName} (Capacity: {Capacity}) found queue full upon execution. Load {Load} will be dropped.",
                OwningQueue.Name, OwningQueue.Configuration.Capacity, LoadToEnqueue);
            // OwningQueue.InvokeBalkActions(LoadToEnqueue, engine.ClockTime); // If such a method exists
            return;
        }

        double currentTime = engine.ClockTime;

        OwningQueue.Waiting.Add(LoadToEnqueue);
        OwningQueue.TimeBasedMetric.ObserveChange(1, currentTime); // Update based on actual count change
        OwningQueue.Logger.LogTrace("Enqueued {Load} into {QueueName} at {Time}. New Occupancy: {Occupancy}",
            LoadToEnqueue, OwningQueue.Name, currentTime, OwningQueue.Occupancy);

        foreach (var action in OwningQueue.OnStateChangeActions)
        {
            action(currentTime);
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{OwningQueue.Name}_Enqueue({LoadToEnqueue})";
}

/// <summary>
/// Event that updates the dequeueing permission state of the <see cref="AbstractQueueEvent{TLoad}.OwningQueue"/>.
/// </summary>
/// <typeparam name="TLoad">The type of load managed by the queue.</typeparam>
internal sealed class UpdateToDequeueEvent<TLoad> : AbstractQueueEvent<TLoad>
{
    /// <summary>
    /// Gets the new state for whether the queue is permitted to dequeue items.
    /// </summary>
    internal bool NewToDequeueState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateToDequeueEvent{TLoad}"/> class.
    /// </summary>
    /// <param name="owner">The queue whose dequeue state is to be updated.</param>
    /// <param name="newToDequeueState">The new dequeue permission state.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="owner"/> is null.</exception>
    public UpdateToDequeueEvent(Core.Queue<TLoad> owner, bool newToDequeueState) : base(owner)
    {
        NewToDequeueState = newToDequeueState;
    }

    /// <summary>
    /// Sets the queue's dequeue permission and may trigger a dequeue attempt if now permitted.
    /// </summary>
    /// <param name="engine">The simulation run context.</param>
    public override void Execute(IRunContext engine)
    {
        bool oldState = OwningQueue.ToDequeue;
        OwningQueue.SetToDequeueState(NewToDequeueState); // Uses the internal setter in Queue
        OwningQueue.Logger.LogTrace("UpdateToDequeueEvent for {QueueName}: ToDequeue changed from {OldState} to {NewState} at {Time}",
            OwningQueue.Name, oldState, NewToDequeueState, engine.ClockTime);

        if (OwningQueue.ToDequeue && OwningQueue.Occupancy > 0)
        {
            engine.Scheduler.Schedule(new DequeueEvent<TLoad>(OwningQueue), engine.ClockTime);
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{OwningQueue.Name}_UpdateToDequeue({NewToDequeueState})";
}

/// <summary>
/// Event that handles removing a <typeparamref name="TLoad"/> entity from the head of the <see cref="AbstractQueueEvent{TLoad}.OwningQueue"/>.
/// </summary>
/// <typeparam name="TLoad">The type of load being dequeued.</typeparam>
internal sealed class DequeueEvent<TLoad> : AbstractQueueEvent<TLoad>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DequeueEvent{TLoad}"/> class.
    /// </summary>
    /// <param name="owner">The queue from which a load will be dequeued.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="owner"/> is null.</exception>
    public DequeueEvent(Core.Queue<TLoad> owner) : base(owner) { }

    /// <summary>
    /// Removes a load from the queue, updates statistics, invokes actions, and may trigger further dequeues.
    /// </summary>
    /// <param name="engine">The simulation run context.</param>
    public override void Execute(IRunContext engine)
    {
        if (OwningQueue.Occupancy == 0)
        {
            OwningQueue.Logger.LogTrace("DequeueEvent for {QueueName}: Queue is empty. No action.", OwningQueue.Name);
            return;
        }
        if (!OwningQueue.ToDequeue)
        {
            OwningQueue.Logger.LogTrace("DequeueEvent for {QueueName}: Queue is not set to ToDequeue. No action.", OwningQueue.Name);
            return;
        }

        TLoad load = OwningQueue.Waiting[0];
        OwningQueue.Waiting.RemoveAt(0);

        double currentTime = engine.ClockTime;
        OwningQueue.TimeBasedMetric.ObserveChange(-1, currentTime); // Update based on actual count change
        OwningQueue.Logger.LogTrace("Dequeued {Load} from {QueueName} at {Time}. New Occupancy: {Occupancy}",
            load, OwningQueue.Name, currentTime, OwningQueue.Occupancy);

        foreach (var action in OwningQueue.OnDequeueActions)
        {
            action(load, currentTime);
        }

        foreach (var action in OwningQueue.OnStateChangeActions)
        {
            action(currentTime);
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{OwningQueue.Name}_Dequeue";
}