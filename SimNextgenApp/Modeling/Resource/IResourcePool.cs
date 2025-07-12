using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling.Resource;

public interface IResourcePool<TResource> : IWarmupAware
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
    /// Gets the total number of resources in the pool (busy + available).
    /// </summary>
    int TotalCapacity { get; }

    /// <summary>
    /// Gets the number of resources that are currently available for acquisition.
    /// </summary>
    int AvailableCount { get; }

    /// <summary>
    /// Gets the number of resources that are currently acquired (busy).
    /// </summary>
    int BusyCount { get; }

    /// <summary>
    /// Occurs when a resource has been successfully acquired from the pool.
    /// First argument is the acquired resource, second is the simulation time.
    /// </summary>
    event Action<TResource, double>? ResourceAcquired;

    /// <summary>
    /// Occurs when a resource has been released back into the pool.
    /// First argument is the released resource, second is the simulation time.
    /// </summary>
    event Action<TResource, double>? ResourceReleased;

    /// <summary>
    /// Occurs when an attempt to acquire a resource fails because none are available.
    /// The argument is the simulation time of the failed attempt.
    /// </summary>
    event Action<double>? RequestFailed;

    /// <summary>
    /// Attempts to acquire a resource from the pool immediately.
    /// This is a synchronous operation and does not schedule an event.
    /// </summary>
    /// <param name="engineContext">The current run context, providing simulation time.</param>
    /// <returns>A resource instance if one is available; otherwise, <c>null</c>.</returns>
    TResource? TryAcquire(IRunContext engineContext);

    /// <summary>
    /// Releases a previously acquired resource, returning it to the pool.
    /// This is a synchronous operation and does not schedule an event.
    /// </summary>
    /// <param name="resource">The resource to release. Must not be null.</param>
    /// <param name="engineContext">The current run context, providing simulation time.</param>
    void Release(TResource resource, IRunContext engineContext);
}
