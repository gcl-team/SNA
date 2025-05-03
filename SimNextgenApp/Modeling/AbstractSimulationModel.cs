using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling;

public abstract class AbstractSimulationModel : ISimulationModel
{
    private static long _instanceCounter = 0;

    /// <inheritdoc/>
    public long Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public IDictionary<string, object> Metadata { get; }

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

    /// <inheritdoc/>
    /// <remarks>
    /// This method is abstract and MUST be implemented by derived classes
    /// to schedule the specific initial events required by the simulation model.
    /// </remarks>
    public abstract void Initialize(IScheduler scheduler);

    /// <inheritdoc/>
    /// <remarks>
    /// This method is abstract and MUST be implemented by derived classes
    /// to define actions needed after the warm-up period (e.g., resetting statistics).
    /// If no warm-up specific action is needed, provide an empty implementation.
    /// </remarks>
    public abstract void WarmedUp(double simulationTime);

    /// <summary>
    /// Returns a string representation of the simulation model instance.
    /// </summary>
    /// <returns>A string in the format "Name#Id".</returns>
    public override string ToString() => $"{Name}#{Id}";
}