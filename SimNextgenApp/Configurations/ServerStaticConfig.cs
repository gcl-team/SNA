namespace SimNextgenApp.Configurations;

/// <summary>
/// Represents the static configuration settings for a server component in the simulation.
/// This configuration defines how the server processes loads (entities), including its
/// capacity and the time it takes to service each load.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) that this server will process.</typeparam>
public record ServerStaticConfig<TLoad> : IStaticConfig
{
    /// <summary>
    /// Gets the maximum number of loads the server can process or hold simultaneously.
    /// </summary>
    public int Capacity { get; init; } = int.MaxValue;

    /// <summary>
    /// Gets the function that defines the service time for each load.
    /// This function takes the <typeparamref name="TLoad"/> being processed and a
    /// <see cref="Random"/> number generator instance, and it should return a
    /// <see cref="TimeSpan"/> representing the duration of the service.
    /// This property is essential for the server's operation.
    /// </summary>
    /// <remarks>
    /// The service time can depend on the properties of the specific <typeparamref name="TLoad"/>
    /// and/or use the <see cref="Random"/> instance to model variability.
    /// </remarks>
    public Func<TLoad, Random, TimeSpan> ServiceTime { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerStaticConfig{TLoad}"/> record
    /// with the specified service time function.
    /// The <see cref="Capacity"/> will default to <see cref="int.MaxValue"/> unless
    /// explicitly set using an object initializer.
    /// </summary>
    /// <param name="serviceTime">
    /// The function that defines how long it takes to service a load. Cannot be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="serviceTime"/> is <c>null</c>.
    /// </exception>
    public ServerStaticConfig(Func<TLoad, Random, TimeSpan> serviceTime)
    {
        ArgumentNullException.ThrowIfNull(serviceTime);
        ServiceTime = serviceTime;
    }
}