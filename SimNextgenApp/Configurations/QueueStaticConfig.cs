namespace SimNextgenApp.Configurations;

/// <summary>
/// Represents the static configuration settings for a queue component in the simulation.
/// This configuration primarily defines the queue's capacity.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) that this queue will hold.</typeparam>
/// <remarks>
/// This configuration assumes a FIFO queueing discipline by default.
/// </remarks>
public record QueueStaticConfig<TLoad> : IStaticConfig
{
    /// <summary>
    /// Gets the maximum number of loads the queue can hold simultaneously.
    /// </summary>
    public int Capacity { get; init; } = int.MaxValue;
}
