using System.Diagnostics;
using System.Diagnostics.Metrics;
using SimNextgenApp.Modeling.Resource;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Observability;

/// <summary>
/// A beginner-friendly convenience API that tracks a resource pool's performance metrics
/// and automatically emits OpenTelemetry measurements underneath.
/// </summary>
/// <typeparam name="TResource">The type of resource being managed by the pool.</typeparam>
public class ResourceObserver<TResource> : IDisposable where TResource : notnull
{
    private readonly IResourcePool<TResource> _resourcePool;
    private readonly Meter? _meter;
    private readonly bool _ownsMeter;
    private readonly VolumeEstimator? _volumeEstimator;

    // Local lightweight scalar counters for convenience API
    private int _acquisitionsCount;
    private int _releasesCount;
    private int _failedRequestsCount;

    // Track warmup state explicitly for observable gauge callbacks.
    // Stored as int (0=false, 1=true) to use Interlocked operations for thread-safe cross-thread visibility.
    // Written by simulation thread, read by ObservableGauge callback thread.
    private int _isWarmupPhaseInt = 1; // 1 = true initially

    // OpenTelemetry Instruments
    private readonly Counter<int>? _acquisitionsCounter;
    private readonly Counter<int>? _releasesCounter;
    private readonly Counter<int>? _failedRequestsCounter;
    private ObservableGauge<int>? _availableGauge;
    private ObservableGauge<int>? _inUseGauge;
    private ObservableGauge<double>? _utilizationGauge;

    /// <summary>
    /// Gets the underlying OpenTelemetry Meter for advanced use cases.
    /// </summary>
    public Meter? Meter => _meter;

    /// <summary>
    /// Number of available resources in the pool.
    /// </summary>
    public int Available => _resourcePool.AvailableCount;

    /// <summary>
    /// Number of resources currently allocated (in use).
    /// </summary>
    public int InUse => _resourcePool.BusyCount;

    /// <summary>
    /// Instantaneous utilization of the resource pool (ratio of in-use to total capacity).
    /// (0.0 to 1.0)
    /// </summary>
    public double Utilization
    {
        get
        {
            if (_resourcePool.TotalCapacity == 0) return 0.0;
            return _resourcePool.BusyCount / (double)_resourcePool.TotalCapacity;
        }
    }

    /// <summary>
    /// Total number of successful resource acquisitions.
    /// </summary>
    public int AcquisitionsCount => _acquisitionsCount;

    /// <summary>
    /// Total number of resource releases.
    /// </summary>
    public int ReleasesCount => _releasesCount;

    /// <summary>
    /// Total number of failed acquisition requests (when no resources were available).
    /// </summary>
    public int FailedRequestsCount => _failedRequestsCount;

    internal ResourceObserver(IResourcePool<TResource> resourcePool, Meter? meter, bool ownsMeter = false, VolumeEstimator? volumeEstimator = null)
    {
        _resourcePool = resourcePool ?? throw new ArgumentNullException(nameof(resourcePool));
        _meter = meter;
        _ownsMeter = ownsMeter;
        _volumeEstimator = volumeEstimator;

        if (_meter != null)
        {
            _acquisitionsCounter = _meter.CreateCounter<int>("sna.resource.acquisitions", description: "Total successful resource acquisitions");
            _releasesCounter = _meter.CreateCounter<int>("sna.resource.releases", description: "Total resource releases");
            _failedRequestsCounter = _meter.CreateCounter<int>("sna.resource.failed_requests", description: "Total failed acquisition requests");

            _availableGauge = _meter.CreateObservableGauge<int>(
                "sna.resource.available",
                () =>
                {
                    // Thread-safe read of warmup state using Interlocked
                    bool isWarmup = Interlocked.CompareExchange(ref _isWarmupPhaseInt, 0, 0) != 0;

                    return new Measurement<int>(
                        this.Available,
                        new KeyValuePair<string, object?>("sna.resource.name", _resourcePool.Name),
                        new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));
                },
                description: "Number of available resources in the pool"
            );

            _inUseGauge = _meter.CreateObservableGauge<int>(
                "sna.resource.in_use",
                () =>
                {
                    // Thread-safe read of warmup state using Interlocked
                    bool isWarmup = Interlocked.CompareExchange(ref _isWarmupPhaseInt, 0, 0) != 0;

                    return new Measurement<int>(
                        this.InUse,
                        new KeyValuePair<string, object?>("sna.resource.name", _resourcePool.Name),
                        new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));
                },
                description: "Number of resources currently in use"
            );

            _utilizationGauge = _meter.CreateObservableGauge<double>(
                "sna.resource.utilization",
                () =>
                {
                    // Thread-safe read of warmup state using Interlocked
                    bool isWarmup = Interlocked.CompareExchange(ref _isWarmupPhaseInt, 0, 0) != 0;

                    return new Measurement<double>(
                        this.Utilization,
                        new KeyValuePair<string, object?>("sna.resource.name", _resourcePool.Name),
                        new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));
                },
                description: "Instantaneous utilization of the resource pool"
            );
        }

        Subscribe();
    }

    /// <summary>
    /// Reads the warmup phase state from the current Activity span context.
    /// Returns false if no Activity is active or if the warmup tag is not set.
    /// </summary>
    private bool GetWarmupPhase()
    {
        var activity = Activity.Current;
        if (activity == null) return false;

        var warmupTag = activity.GetTagItem("sna.simulation.warmup");
        return warmupTag as bool? ?? false;
    }

    private void Subscribe()
    {
        _resourcePool.ResourceAcquired += OnResourceAcquired;
        _resourcePool.ResourceReleased += OnResourceReleased;
        _resourcePool.RequestFailed += OnRequestFailed;
    }

    private void OnResourceAcquired(TResource resource, long clockTime)
    {
        _acquisitionsCount++;
        bool isWarmup = GetWarmupPhase();

        // Update warmup state for observable gauge callbacks (which run on background threads)
        // Use Interlocked for thread-safe write with memory barrier guarantees
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        if (_acquisitionsCounter != null)
        {
            _acquisitionsCounter.Add(1,
                new KeyValuePair<string, object?>("sna.resource.name", _resourcePool.Name),
                new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));

            // Track metric data point for volume estimation
            _volumeEstimator?.RecordMetricDataPoint();
        }
    }

    private void OnResourceReleased(TResource resource, long clockTime)
    {
        _releasesCount++;
        bool isWarmup = GetWarmupPhase();

        // Update warmup state for observable gauge callbacks (which run on background threads)
        // Use Interlocked for thread-safe write with memory barrier guarantees
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        if (_releasesCounter != null)
        {
            _releasesCounter.Add(1,
                new KeyValuePair<string, object?>("sna.resource.name", _resourcePool.Name),
                new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));

            // Track metric data point for volume estimation
            _volumeEstimator?.RecordMetricDataPoint();
        }
    }

    private void OnRequestFailed(long clockTime)
    {
        _failedRequestsCount++;
        bool isWarmup = GetWarmupPhase();

        // Update warmup state for observable gauge callbacks (which run on background threads)
        // Use Interlocked for thread-safe write with memory barrier guarantees
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        if (_failedRequestsCounter != null)
        {
            _failedRequestsCounter.Add(1,
                new KeyValuePair<string, object?>("sna.resource.name", _resourcePool.Name),
                new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));

            // Track metric data point for volume estimation
            _volumeEstimator?.RecordMetricDataPoint();
        }
    }

    /// <summary>
    /// Creates a simple observer that uses the default OpenTelemetry meter.
    /// This is the primary beginner-friendly entry point.
    /// </summary>
    /// <remarks>
    /// The observer creates a dedicated meter instance that is disposed when the observer is disposed,
    /// preventing memory leaks when running multiple simulations.
    /// </remarks>
    public static ResourceObserver<TResource> CreateSimple(IResourcePool<TResource> resourcePool)
    {
        // Create a new meter for this observer to avoid memory leaks from static shared meters
        var meter = new Meter(SimulationTelemetry.MeterName);
        return new ResourceObserver<TResource>(resourcePool, meter, ownsMeter: true);
    }

    public void Dispose()
    {
        _resourcePool.ResourceAcquired -= OnResourceAcquired;
        _resourcePool.ResourceReleased -= OnResourceReleased;
        _resourcePool.RequestFailed -= OnRequestFailed;

        // Dispose the meter if we own it
        // This disposes all instruments created on the meter, including the observable gauge callback
        if (_ownsMeter)
        {
            _meter?.Dispose();
        }
    }
}

public static class ResourceObserver
{
    // Helper to avoid specifying TResource for type inference where possible, or factory methods
    public static ResourceObserver<TResource> CreateSimple<TResource>(IResourcePool<TResource> resourcePool) where TResource : notnull
    {
        return ResourceObserver<TResource>.CreateSimple(resourcePool);
    }
}
