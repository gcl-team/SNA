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
    /// Gets the current simulation time in simulation time units (e.g., seconds, minutes).
    /// </summary>
    double ClockTime { get; }

    /// <summary>
    /// Gets the number of events that have been executed so far in the simulation run.
    /// </summary>
    long ExecutedEventCount { get; }
}