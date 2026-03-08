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
    /// Gets the current simulation clock time as an integer count of simulation time units.
    /// This is the time of the event currently being processed.
    /// The unit (seconds, milliseconds, etc.) is defined by the simulation profile's TimeUnit setting.
    /// </summary>
    long ClockTime { get; }

    /// <summary>
    /// Schedules a simulation event to occur at a specific absolute simulation time.
    /// </summary>
    /// <param name="ev">The event object to schedule.</param>
    /// <param name="time">The absolute simulation time (in simulation units) at which the event should execute.
    /// Must be greater than or equal to the current ClockTime.</param>
    /// <remarks>
    /// The time parameter is interpreted in the simulation's configured time units.
    /// For example, if TimeUnit is Milliseconds, time=5000 means the event will execute at 5000 milliseconds.
    /// </remarks>
    void Schedule(AbstractEvent ev, long time);

    /// <summary>
    /// Schedules a simulation event to occur after a specified delay from the current simulation time.
    /// The <see cref="TimeSpan"/> delay is automatically converted to the simulation's configured time units.
    /// </summary>
    /// <param name="ev">The event object to schedule. Must not be null.</param>
    /// <param name="delay">
    /// The duration from the current simulation time after which the event should be processed.
    /// Must be a non-negative <see cref="TimeSpan"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method provides a convenient way to schedule events using user-friendly TimeSpan values.
    /// The delay is converted to simulation units based on the profile's TimeUnit setting using
    /// <see cref="Core.Utilities.TimeUnitConverter.ConvertToSimulationUnits"/>.
    /// </para>
    /// <para>
    /// Fractional parts are truncated. For sub-unit precision, configure a finer TimeUnit.
    /// For example, use TimeUnit.Milliseconds instead of TimeUnit.Seconds if you need millisecond precision.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="ev"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="delay"/> is negative.</exception>
    void Schedule(AbstractEvent ev, TimeSpan delay);
}