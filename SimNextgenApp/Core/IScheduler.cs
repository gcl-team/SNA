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
    void Schedule(AbstractEvent ev, double time); // <-- Uses correct Event base and double time
}