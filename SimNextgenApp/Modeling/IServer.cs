using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling;

public interface IServer<TLoad> : IWarmupAware
{
    // <summary>
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
    /// Attempts to start serving the given load if capacity is available.
    /// If successful, the server's state is updated and a service completion event is scheduled.
    /// </summary>
    /// <param name="loadToServe">The load to start serving.</param>
    /// <returns><c>true</c> if the load could be accepted (i.e., vacancy > 0 and event scheduled); <c>false</c> otherwise.</returns>
    /// <exception cref="InvalidOperationException">Thrown if Initialize has not been called yet.</exception>
    /// <exception cref="ArgumentNullException">Thrown if engine or loadToServe is null.</exception>
    bool TryStartService(TLoad loadToServe);

    // --- Event Hooks (events for observation) ---
    /// <summary>
    /// Actions to execute when a load departs from the server after completing service.
    /// Provides the departed load and its service completion time.
    /// </summary>
    event Action<TLoad, double> LoadDeparted;

    /// <summary>
    /// Actions to execute when the server's state changes (e.g., becomes busy, becomes idle, load departs).
    /// Provides the current simulation time.
    /// </summary>
    event Action<double> StateChanged;
}
