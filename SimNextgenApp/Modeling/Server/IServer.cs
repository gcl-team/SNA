using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling.Server;

/// <summary>
/// Defines the public, read-only view of a server: a component that serves up to a fixed number
/// of loads of type <typeparamref name="TLoad"/> at a time, each for a sampled service time.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) served by the server.</typeparam>
public interface IServer<TLoad> : IWarmupAware
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
    /// Gets the configured capacity of the server.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets the number of loads currently being processed by the server.
    /// </summary>
    int NumberInService { get; }

    /// <summary>
    /// Gets the number of available slots for new loads, based on the server's capacity.
    /// </summary>
    int Vacancy { get; }

    /// <summary>
    /// Gets the set of loads currently being processed (in service) by the server.
    /// </summary>
    IReadOnlySet<TLoad> LoadsInService { get; }

    /// <summary>
    /// Gets the simulation time when the specified load started service, if it's currently being processed.
    /// Used for calculating sojourn time when the load departs.
    /// </summary>
    /// <param name="load">The load to query.</param>
    /// <returns>The simulation time when service started, or null if the load is not currently in service.</returns>
    long? GetServiceStartTime(TLoad load);

    /// <summary>
    /// Attempts to start serving the given load if capacity is available.
    /// If successful, the server's state is updated and a service completion event is scheduled.
    /// </summary>
    /// <param name="loadToServe">The load to start serving.</param>
    /// <param name="engineContext">The current run context (provides time and scheduler).</param>
    /// <returns><c>true</c> if the load could be accepted (i.e., vacancy > 0 and event scheduled); <c>false</c> otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown if loadToServe or engineContext is null.</exception>
    bool TryStartService(TLoad loadToServe, IRunContext engineContext);

    /// <summary>
    /// Actions to execute when a load departs from the server after completing service.
    /// Provides the departed load and its service completion time.
    /// </summary>
    event Action<TLoad, long> LoadDeparted;

    /// <summary>
    /// Actions to execute when the server's state changes (e.g., becomes busy, becomes idle, load departs).
    /// Provides the current simulation time.
    /// </summary>
    event Action<long> StateChanged;
}
