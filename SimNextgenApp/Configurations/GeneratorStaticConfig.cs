namespace SimNextgenApp.Configurations;

/// <summary>
/// Represents the static configuration settings for a load generator in the simulation.
/// This configuration defines how and when new loads (entities) are created and introduced
/// into the simulation.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) that this generator will create.</typeparam>
public record GeneratorStaticConfig<TLoad> : IStaticConfig
{
    /// <summary>
    /// Gets the function used to determine the time interval between consecutive load arrivals.
    /// This function takes a <see cref="Random"/> number generator instance and should return
    /// a <see cref="TimeSpan"/> representing the time until the next arrival.
    /// This property is essential for the generator's operation.
    /// </summary>
    public Func<Random, TimeSpan> InterArrivalTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether the first generated load should be skipped.
    /// This can be useful for simulation warm-up periods, allowing the system to reach
    /// a more stable state before data collection begins.
    /// Defaults to <c>true</c> (skip the first load).
    /// </summary>
    public bool IsSkippingFirst { get; init; } = true;

    /// <summary>
    /// Gets the factory function used to create new instances of <typeparamref name="TLoad"/>.
    /// This function takes a <see cref="Random"/> number generator instance (which can be used
    /// to initialise random properties of the load) and should return a new load instance.
    /// This property is essential for the generator's operation.
    /// </summary>
    public Func<Random, TLoad> LoadFactory { get; init; }

    /// <summary>
    /// Initialises a new instance of the <see cref="GeneratorStaticConfig{TLoad}"/> record
    /// with the specified inter-arrival time function and load factory function.
    /// </summary>
    /// <param name="interArrivalTime">
    /// The function that defines the time between load arrivals. Cannot be null.
    /// </param>
    /// <param name="loadFactory">
    /// The function that creates new load instances. Cannot be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="interArrivalTime"/> or <paramref name="loadFactory"/> is <c>null</c>.
    /// </exception>
    public GeneratorStaticConfig(
        Func<Random, TimeSpan> interArrivalTime,
        Func<Random, TLoad> loadFactory)
    {
        ArgumentNullException.ThrowIfNull(interArrivalTime);
        ArgumentNullException.ThrowIfNull(loadFactory);

        InterArrivalTime = interArrivalTime;
        LoadFactory = loadFactory;
        // IsSkippingFirst will use its default (true) unless explicitly set during object initialisation.
    }
}