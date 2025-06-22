using SimNextgenApp.Events;

namespace SimNextgenApp.Core;

/// <summary>
/// Defines the interface for scheduling events, provided by the SimulationEngine
/// to the ISimulationModel during initialization. This decouples the model
/// from the engine's internal event list implementation.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Schedules a simulation event to occur at a specific future simulation time.
    /// </summary>
    /// <param name="ev">The event object to schedule.</param>
    /// <param name="time">The absolute simulation time (using simulation time units) at which the event should execute.</param>
    void Schedule(AbstractEvent ev, double time);

    /// <summary>
    /// Schedules a simulation event to occur after a specified delay from the current simulation time.
    /// The simulation engine, which implements this interface, will convert the <see cref="TimeSpan"/>
    /// delay into its internal simulation time units.
    /// </summary>
    /// <param name="ev">The event object to schedule. Must not be null.</param>
    /// <param name="delay">
    /// The duration from the current simulation time after which the event should be processed.
    /// Must be a non-negative <see cref="TimeSpan"/>.
    /// </param>
    /// <remarks>
    /// This method provides a convenient way to schedule events based on relative durations
    /// without needing to know the engine's internal time unit scale directly.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="ev"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="delay"/> is negative.</exception>
    void Schedule(AbstractEvent ev, TimeSpan time);
}