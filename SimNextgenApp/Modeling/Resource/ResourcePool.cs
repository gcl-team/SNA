using Microsoft.Extensions.Logging;
using SimNextgenApp.Core;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Modeling.Resource;

public class ResourcePool<TResource> : AbstractSimulationModel, IResourcePool<TResource>
    where TResource : notnull
{
    private readonly List<TResource> _idleResources;
    private readonly ILogger<ResourcePool<TResource>> _logger;

    public event Action<TResource, double>? ResourceAcquired;
    public event Action<TResource, double>? ResourceReleased;
    public event Action<double>? RequestFailed;

    public int TotalCapacity { get; }
    public int AvailableCount => _idleResources.Count;
    public int BusyCount => TotalCapacity - AvailableCount;

    /// <summary>
    /// Gets the utilisation metric, which represents time-based performance data for the resource pool.
    /// </summary>
    public TimeBasedMetric UtilizationMetric { get; }

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
        UtilizationMetric = new TimeBasedMetric(enableHistory: false);
        _logger.LogInformation("ResourcePool '{PoolName}' created with capacity {Capacity}.", Name, TotalCapacity);
    }

    /// <inheritdoc/>
    public TResource? TryAcquire(IRunContext engineContext)
    {
        double currentTime = engineContext.ClockTime;

        if (AvailableCount > 0)
        {
            var resource = _idleResources[^1];
            _idleResources.RemoveAt(_idleResources.Count - 1);

            UtilizationMetric.ObserveCount(BusyCount, currentTime);
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

        double currentTime = engineContext.ClockTime;

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
        UtilizationMetric.ObserveCount(BusyCount, currentTime);
        _logger.LogTrace("Resource released to '{PoolName}' at {Time}. Available: {Available}", Name, currentTime, AvailableCount);

        ResourceReleased?.Invoke(resource, currentTime);
    }

    /// <summary>
    /// Initialises the current instance with the specified run context.
    /// </summary>
    /// <remarks>This method sets up the necessary state for the instance to function within the provided test
    /// run context. It is typically called at the start of a test run and should not be invoked multiple times without
    /// proper cleanup.</remarks>
    /// <param name="runContext">The context for the current test run, providing access to run-specific data and settings.</param>
    public override void Initialize(IRunContext runContext)
    {
        UtilizationMetric.ObserveCount(0, 0);
    }

    /// <inheritdoc/>
    public override void WarmedUp(double simulationTime)
    {
        UtilizationMetric.WarmedUp(simulationTime);
        UtilizationMetric.ObserveCount(BusyCount, simulationTime);
    }
}
