namespace SimNextgenApp.Configurations;

/// <summary>
/// Represents the static configuration settings for a load queue in the simulation.
/// This primarily defines the capacity limits of the queue.
/// </summary>
public record LoadQueueStaticConfig : IStaticConfig
{
    /// <summary>
    /// Gets the maximum number of loads (items) that the queue can hold.
    /// A value of <see cref="int.MaxValue"/> (the default) typically indicates
    /// that the queue has effectively unlimited capacity.
    /// </summary>
    public int Capacity { get; init; } = int.MaxValue;
}