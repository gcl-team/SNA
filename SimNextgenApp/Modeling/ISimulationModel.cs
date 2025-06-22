using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling;

/// <summary>
/// Defines the contract for a simulation model that can be executed by the SimulationEngine.
/// Implement this interface to create specific simulation models (e.g., MM1 Queue, Job Shop).
/// </summary>
public interface ISimulationModel
{
    /// <summary>
    /// Gets the unique identifier assigned to this simulation model instance.
    /// Typically assigned during instantiation.
    /// </summary>
    long Id { get; }

    /// <summary>
    /// Gets a descriptive, human-readable name for this simulation model instance.
    /// Useful for logging and results reporting. Typically set during instantiation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a dictionary for storing extensible metadata associated with this model instance.
    /// Use this to store information beyond Id and Name, such as "Description", "Version",
    /// "Author", "ScenarioGroup", or custom tags. The dictionary instance should be
    /// provided by the implementing class, typically initialized in its constructor.
    /// </summary>
    /// <example>
    /// Metadata["Description"] = "Baseline model for system X";
    /// Metadata["Version"] = "1.2";
    /// </example>
    IReadOnlyDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Called by the SimulationEngine BEFORE the main simulation run loop begins.
    /// Implementations should use this method to schedule the initial events
    /// required to start the simulation (e.g., the first entity arrival,
    /// initial state setup events).
    /// </summary>
    /// <param name="scheduler">An interface provided by the engine, allowing the model
    /// to schedule events without directly accessing the FEL.</param>
    void Initialize(IScheduler scheduler);
}