namespace SimNextgenApp.Core.Strategies;

/// <summary>
/// Defines the contract for strategies that control the execution duration
/// and warm-up period of a simulation run.
/// </summary>
public interface IRunStrategy
{
    /// <summary>
    /// Gets the simulation time at which the warm-up period ends.
    /// Returns null if no warm-up period is defined for this strategy.
    /// The SimulationEngine will call ISimulationModel.WarmedUp() when
    /// ClockTime first reaches or exceeds this value.
    /// </summary>
    /// <example>Return 1000.0 to indicate warm-up ends at simulation time 1000.</example>
    double? WarmupEndTime { get; } // Use double, nullable

    /// <summary>
    /// Determines whether the simulation engine should continue processing the next event.
    /// Called by the SimulationEngine at the start of each iteration of the main event loop.
    /// </summary>
    /// <param name="runContext">The simulation running context, providing access to current state like ClockTime.</param>
    /// <returns>true if the simulation should continue; false if the simulation should stop.</returns>
    bool ShouldContinue(IRunContext runContext);
}