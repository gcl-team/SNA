namespace SimNextgenApp.Core;

/// <summary>
/// Defines a contract for simulation models that are aware of and can react to
/// the completion of a simulation warm-up period.
/// </summary>
public interface IWarmupAware
{
    /// <summary>
    /// Called by the SimulationEngine AFTER a defined warm-up period has completed.
    /// Implementations should use this to perform actions like resetting statistics
    /// accumulators to ignore the transient warm-up phase.
    /// </summary>
    /// <param name="simulationTime">The current simulation clock time at the moment the warm-up period ends.</param>
    void WarmedUp(double simulationTime);
}
