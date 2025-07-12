using SimNextgenApp.Core;
using SimNextgenApp.Modeling.Queue;

namespace SimNextgenApp.Events;

/// <summary>
/// Base class for events specific to the internal operations of a <see cref="SimQueue{TLoad}"/>.
/// </summary>
/// <typeparam name="TLoad">The type of load managed by the queue.</typeparam>
internal abstract class AbstractQueueEvent<TLoad> : AbstractEvent
{
    /// <summary>
    /// Gets the queue instance that this event pertains to.
    /// </summary>
    internal ISimQueue<TLoad> OwningQueue { get; }

    public override IDictionary<string, object>? GetTraceDetails()
    {
        return new Dictionary<string, object>
        {
            { "GeneratorName", OwningQueue.Name },
            { "Vacancy", OwningQueue.Vacancy },
            { "Occupancy", OwningQueue.Occupancy },
            { "Capacity", OwningQueue.Capacity }
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractQueueEvent{TLoad}"/> class.
    /// </summary>
    /// <param name="owner">The queue that owns this event.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="owner"/> is null.</exception>
    protected AbstractQueueEvent(SimQueue<TLoad> owner)
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
    public EnqueueEvent(SimQueue<TLoad> owner, TLoad loadToEnqueue) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(loadToEnqueue, nameof(loadToEnqueue));
        LoadToEnqueue = loadToEnqueue;
    }

    /// <summary>
    /// Executes the enqueue operation by delegating to the owning queue handler method.
    /// </summary>
    /// <param name="engine">The simulation run context.</param>
    public override void Execute(IRunContext engine)
    {
        if (OwningQueue is IOperatableQueue<TLoad> operatableQueue)
        {
            operatableQueue.HandleEnqueue(LoadToEnqueue, engine.ClockTime);
        }
        else
        {
            throw new InvalidOperationException($"The queue '{OwningQueue.Name}' does not implement IOperatableQueue and cannot handle this event.");
        }
    }

    public override string ToString() => $"{OwningQueue.Name}_Enqueue({LoadToEnqueue})#{EventId} @ {ExecutionTime:F4}";
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
    public DequeueEvent(SimQueue<TLoad> owner) : base(owner) { }

    /// <summary>
    /// Executes the dequeue operation by delegating to the owning queue handler method.
    /// </summary>
    /// <param name="engine">The simulation run context.</param>
    public override void Execute(IRunContext engine)
    {
        if (OwningQueue is IOperatableQueue<TLoad> operatableQueue)
        {
            operatableQueue.HandleDequeue(engine.ClockTime);
        }
        else
        {
            throw new InvalidOperationException($"The queue '{OwningQueue.Name}' does not implement IOperatableQueue and cannot handle this event.");
        }
    }

    public override string ToString() => $"{OwningQueue.Name}_Dequeue#{EventId} @ {ExecutionTime:F4}";
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
    public UpdateToDequeueEvent(SimQueue<TLoad> owner, bool newToDequeueState) : base(owner)
    {
        NewToDequeueState = newToDequeueState;
    }

    /// <summary>
    /// Executes the dequeue permission update operation by delegating to the owning queue handler method.
    /// </summary>
    /// <param name="engine">The simulation run context.</param>
    public override void Execute(IRunContext engine)
    {
        if (OwningQueue is IOperatableQueue<TLoad> operatableQueue)
        {
            operatableQueue.HandleUpdateToDequeue(NewToDequeueState, engine.ClockTime);
        }
        else
        {
            throw new InvalidOperationException($"The queue '{OwningQueue.Name}' does not implement IOperatableQueue and cannot handle this event.");
        }
    }

    public override string ToString() => $"{OwningQueue.Name}_UpdateToDequeue({NewToDequeueState})";
}
