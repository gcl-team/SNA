using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling;

/// <summary>
/// Provides a foundational abstract base class for all simulation models within the framework.
/// This class handles common functionalities such as unique instance identification, naming,
/// metadata storage, and defines the core lifecycle methods that concrete models must implement.
/// </summary>
public abstract class AbstractSimulationModel : ISimulationModel, IWarmupAware
{
    private static long _instanceCounter = 0;

    /// <inheritdoc/>
    public long Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>
    /// Gets a collection of metadata associated with the current object.
    /// </summary>
    /// <remarks>The metadata can be used to store additional information about the object, such as custom
    /// attributes  or contextual data. The dictionary is read-only and cannot be modified directly.</remarks>
    public IDictionary<string, object> Metadata { get; }

    /// <inheritdoc/>
    IReadOnlyDictionary<string, object> ISimulationModel.Metadata => (IReadOnlyDictionary<string, object>)Metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractSimulationModel"/> class.
    /// </summary>
    /// <param name="name">A descriptive name for this model instance. Cannot be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown if name is null or whitespace.</exception>
    protected AbstractSimulationModel(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Model name cannot be null or whitespace.", nameof(name));
        }

        Id = (int)Interlocked.Increment(ref _instanceCounter);
        Name = name;
        Metadata = new Dictionary<string, object>();
    }

    /// <summary>
    /// Initializes the model, allowing it to schedule initial events.
    /// This method is optional and should be overridden by models that need
    /// to perform startup actions.
    /// </summary>
    /// <param name="context">The simulation run context.</param>
    public virtual void Initialize(IRunContext context)
    {
        // Default implementation is empty.
        // Passive models do not need to override this.
        // Active models (like a Generator) WILL override this to schedule their first event.
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This method is abstract and MUST be implemented by derived classes
    /// to define actions needed after the warm-up period (e.g., resetting statistics).
    /// If no warm-up specific action is needed, provide an empty implementation.
    /// </remarks>
    public virtual void WarmedUp(double simulationTime) 
    {
        // Default implementation does nothing.
        // Derived classes can override this to perform actions after warm-up.
    }

    /// <summary>
    /// Returns a string representation of the simulation model instance.
    /// </summary>
    /// <returns>A string in the format "Name#Id".</returns>
    public override string ToString() => $"{Name}#{Id}";
}