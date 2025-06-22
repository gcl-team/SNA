using SimNextgenApp.Core;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SimNextgenApp.Tests")] // Allow unit tests to access internal members
namespace SimNextgenApp.Events;

/// <summary>
/// Abstract base class for all discrete events in the simulation.
/// Events represent points in time where the state of the simulation changes.
/// </summary>
public abstract class AbstractEvent
{
    // Static counter for generating unique event IDs
    private static long _eventCounter = 0;

    /// <summary>
    /// Gets the unique identifier for this specific event instance.
    /// Useful for tracing and debugging.
    /// </summary>
    public long EventId { get; } = Interlocked.Increment(ref _eventCounter);

    /// <summary>
    /// The simulation time at which this event is scheduled to occur.
    /// Set by the SimulationEngine when the event is scheduled.
    /// </summary>
    public double ExecutionTime { get; internal set; }

    // Optional: Add priority for more complex tie-breaking if needed
    // public virtual int Priority { get; protected set; } = 0; // Lower value = higher priority

    /// <summary>
    /// Executes the logic associated with this specific event.
    /// This method is called by the SimulationEngine when the simulation clock
    /// reaches the event's ExecutionTime.
    /// </summary>
    /// <param name="engine">A reference to the simulation engine, providing access
    /// to the current state (ClockTime) and core services like the scheduler (IScheduler).
    /// </param>
    public abstract void Execute(IRunContext engine);

    /// <summary>
    /// Provides a dictionary of key-value pairs representing the specific details of this event instance.
    /// Useful for structured tracing and deep debugging.
    /// </summary>
    /// <returns>A dictionary of details, or null if there are none.</returns>
    public virtual IDictionary<string, object>? GetTraceDetails() => null;

    /// <summary>
    /// Provides a basic string representation, useful for debugging/logging.
    /// Derived classes should override this for more specific information.
    /// </summary>
    public override string ToString()
    {
        // Basic representation, derived classes can add associated entity IDs etc.
        return $"{GetType().Name}#{EventId} @ {ExecutionTime:F4}";
    }
}