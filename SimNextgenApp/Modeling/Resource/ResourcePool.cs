using Microsoft.Extensions.Logging;
using SimNextgenApp.Core;

namespace SimNextgenApp.Modeling.Resource;

public class ResourcePool<TResource> : AbstractSimulationModel, IResourcePool<TResource>
    where TResource : notnull
{
    private readonly List<TResource> _idleResources;
    private readonly ILogger<ResourcePool<TResource>> _logger;

    public event Action<TResource, long>? ResourceAcquired;
    public event Action<TResource, long>? ResourceReleased;
    public event Action<long>? RequestFailed;

    public int TotalCapacity { get; }
    public int AvailableCount => _idleResources.Count;
    public int BusyCount => TotalCapacity - AvailableCount;

    /// <summary>
    /// Initialises a new instance of the <see cref="ResourcePool{TResource}"/> class with the specified resources,
    /// instance name, and logger factory.
    /// </summary>
    /// <param name="resources">The collection of resources to be managed by the pool. Cannot be null.</param>
    /// <param name="instanceName">The name of the resource pool instance. Used for identification and logging purposes.</param>
    /// <param name="loggerFactory">The factory used to create loggers for the resource pool. Cannot be null.</param>
    public ResourcePool(IEnumerable<TResource> resources, string instanceName, ILoggerFactory loggerFactory)
        : base(instanceName)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _idleResources = new List<TResource>(resources);
        TotalCapacity = _idleResources.Count;
        _logger = loggerFactory.CreateLogger<ResourcePool<TResource>>();
        _logger.LogInformation("ResourcePool '{PoolName}' created with capacity {Capacity}.", Name, TotalCapacity);
    }

    /// <inheritdoc/>
    public TResource? TryAcquire(IRunContext engineContext)
    {
        long currentTime = engineContext.ClockTime;

        if (AvailableCount > 0)
        {
            var resource = _idleResources[^1];
            _idleResources.RemoveAt(_idleResources.Count - 1);

            _logger.LogTrace("Resource acquired from '{PoolName}' at {Time}. Available: {Available}", Name, currentTime, AvailableCount);

            ResourceAcquired?.Invoke(resource, currentTime);
            return resource;
        }

        _logger.LogWarning("Failed to acquire resource from '{PoolName}' at {Time}. None available.", Name, currentTime);

        RequestFailed?.Invoke(currentTime);
        return default;
    }

    /// <inheritdoc/>
    public void Release(TResource resource, IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(resource);

        long currentTime = engineContext.ClockTime;

        if (_idleResources.Count >= TotalCapacity)
        {
            _logger.LogError("CRITICAL: Attempted to release resource back to '{PoolName}' which is already full. Check for double-release logic.", Name);
            return;
        }

        if (_idleResources.Contains(resource))
        {
            _logger.LogError("CRITICAL: Attempted to release resource '{Resource}' which is already in the idle pool for '{PoolName}'. Double-release detected.", resource, Name);
            return;
        }

        _idleResources.Add(resource);

        _logger.LogTrace("Resource released to '{PoolName}' at {Time}. Available: {Available}", Name, currentTime, AvailableCount);

        ResourceReleased?.Invoke(resource, currentTime);
    }

    /// <inheritdoc/>
    public override void Initialize(IRunContext runContext)
    {
        ArgumentNullException.ThrowIfNull(runContext);

        _logger.LogInformation("ResourcePool '{PoolName}' initialized.", Name);
    }

    /// <inheritdoc/>
    public override void WarmedUp(long simulationTime)
    {
        _logger.LogInformation("ResourcePool '{PoolName}' warmed up at {Time}. Current utilization: {InUse}/{Total}",
            Name, simulationTime, BusyCount, TotalCapacity);
    }
}
