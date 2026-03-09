using SimNextgenApp.Core.Strategies;

namespace SimNextgenApp.Core;

/// <summary>
/// Defines the read-only context available to an <see cref="IRunStrategy"/> during a simulation run.
/// Provides access to key simulation state such as the current simulation time and the number of executed events.
/// </summary>
/// <remarks>
/// This abstraction allows run strategies to operate independently of the full <see cref="SimulationEngine"/> implementation,
/// making them easier to test and reuse.
/// </remarks>
public interface IRunContext
{
    /// <summary>
    /// Gets the current simulation clock time as an integer count of simulation time units.
    /// The meaning of one unit is defined by the <see cref="SimulationProfile"/>'s TimeUnit setting
    /// (e.g., if TimeUnit is Seconds, ClockTime=5000 means 5000 seconds).
    /// </summary>
    /// <remarks>
    /// This value is always a non-negative integer representing elapsed simulation time in the
    /// units specified by the simulation profile. It is NOT tied to any physical time unit like
    /// clock ticks or milliseconds - the unit is entirely determined by the simulation configuration.
    /// </remarks>
    long ClockTime { get; }

    /// <summary>
    /// Gets the number of events that have been executed so far in the simulation run.
    /// </summary>
    long ExecutedEventCount { get; }

    /// <summary>
    /// Gets the scheduler used to manage and execute tasks.
    /// </summary>
    IScheduler Scheduler { get; }
}