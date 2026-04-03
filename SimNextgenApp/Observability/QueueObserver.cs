using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Observability;

/// <summary>
/// A beginner-friendly convenience API that tracks a queue's performance metrics
/// and automatically emits OpenTelemetry measurements underneath.
/// </summary>
/// <typeparam name="TLoad">The type of load being managed by the queue.</typeparam>
public class QueueObserver<TLoad> : IDisposable
    where TLoad : notnull
{
    private readonly ISimQueue<TLoad> _queue;
    private readonly Meter? _meter;
    private readonly bool _ownsMeter;
    private readonly VolumeEstimator? _volumeEstimator;
    private SimulationTimeUnit? _timeUnit;

    // Track enqueue times to calculate wait time deltas for emission to OTel Histogram.
    // NOTE: This is NOT internal aggregation - we only calculate the delta (dequeue - enqueue).
    // The observability backend (Grafana/Datadog/etc.) does all aggregations (avg, max, percentiles).
    private readonly ConcurrentDictionary<TLoad, long> _enqueueTimes = new();

    // Local lightweight scalar counters for convenience API
    private int _loadsEnqueued;
    private int _loadsDequeued;
    private int _loadsBalked;

    // Track warmup state explicitly for observable gauge callbacks.
    // Stored as int (0=false, 1=true) to use Interlocked operations for thread-safe cross-thread visibility.
    // Written by simulation thread, read by ObservableGauge callback thread.
    private int _isWarmupPhaseInt = 1; // 1 = true initially

    // Track disposed state to prevent observable gauge callbacks after disposal
    // Stored as int (0=false, 1=true) for thread-safe access from callback threads
    private int _disposedInt = 0; // 0 = false initially

    // OpenTelemetry Instruments
    private readonly Counter<int>? _loadsEnqueuedCounter;
    private readonly Counter<int>? _loadsDequeuedCounter;
    private readonly Counter<int>? _loadsBalkedCounter;
    private readonly Histogram<double>? _waitTimeHistogram;

    /// <summary>
    /// Gets the underlying OpenTelemetry Meter for advanced use cases.
    /// </summary>
    public Meter? Meter => _meter;

    /// <summary>
    /// Total number of loads enqueued.
    /// </summary>
    public int LoadsEnqueued => _loadsEnqueued;

    /// <summary>
    /// Total number of loads dequeued.
    /// </summary>
    public int LoadsDequeued => _loadsDequeued;

    /// <summary>
    /// Total number of loads balked (rejected due to full queue).
    /// </summary>
    public int LoadsBalked => _loadsBalked;

    /// <summary>
    /// Current number of items waiting in the queue.
    /// </summary>
    public int Occupancy => _queue.Occupancy;

    /// <summary>
    /// Sets the simulation time unit for proper wait time conversion.
    /// Should be called during model initialization when the time unit is known.
    /// </summary>
    public void SetTimeUnit(SimulationTimeUnit timeUnit)
    {
        _timeUnit = timeUnit;
    }

    internal QueueObserver(ISimQueue<TLoad> queue, Meter? meter, bool ownsMeter = false, VolumeEstimator? volumeEstimator = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _meter = meter;
        _ownsMeter = ownsMeter;
        _volumeEstimator = volumeEstimator;

        if (_meter != null)
        {
            _loadsEnqueuedCounter = _meter.CreateCounter<int>("sna.queue.loads_enqueued", description: "Total loads enqueued in the queue");
            _loadsDequeuedCounter = _meter.CreateCounter<int>("sna.queue.loads_dequeued", description: "Total loads dequeued from the queue");
            _loadsBalkedCounter = _meter.CreateCounter<int>("sna.queue.loads_balked", description: "Total loads balked (rejected due to full queue)");
            _waitTimeHistogram = _meter.CreateHistogram<double>("sna.queue.wait_time", unit: "s", description: "Time spent by an entity waiting in the queue");

            _meter.CreateObservableGauge<int>(
                "sna.queue.occupancy",
                () =>
                {
                    // Guard against callbacks after disposal (prevents memory leak when using shared meters)
                    // Thread-safe read using Interlocked
                    bool isDisposed = Interlocked.CompareExchange(ref _disposedInt, 0, 0) != 0;
                    if (isDisposed)
                    {
                        return []; // Return no measurements if disposed (avoids unlabeled time series)
                    }

                    // Thread-safe read of warmup state using Interlocked
                    bool isWarmup = Interlocked.CompareExchange(ref _isWarmupPhaseInt, 0, 0) != 0;

                    return
                    [
                        new Measurement<int>(
                            _queue.Occupancy,
                            new KeyValuePair<string, object?>("sna.queue.name", _queue.Name),
                            new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup))
                    ];
                },
                description: "Current number of items waiting in the queue"
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
        if (Activity.Current is not { } activity) return false;

        var warmupTag = activity.GetTagItem("sna.simulation.warmup");
        return warmupTag as bool? ?? false;
    }

    private void Subscribe()
    {
        _queue.LoadEnqueued += OnLoadEnqueued;
        _queue.LoadDequeued += OnLoadDequeued;
        _queue.LoadBalked += OnLoadBalked;
    }

    private void OnLoadEnqueued(TLoad load, long clockTime)
    {
        _loadsEnqueued++;
        bool isWarmup = GetWarmupPhase();

        // Track enqueue time for wait time calculation
        _enqueueTimes[load] = clockTime;

        // Update warmup state for observable gauge callbacks
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        if (_loadsEnqueuedCounter != null)
        {
            _loadsEnqueuedCounter.Add(1,
                new KeyValuePair<string, object?>("sna.queue.name", _queue.Name),
                new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));

            // Track metric data point for volume estimation
            _volumeEstimator?.RecordMetricDataPoint();
        }
    }

    private void OnLoadDequeued(TLoad load, long clockTime)
    {
        _loadsDequeued++;
        bool isWarmup = GetWarmupPhase();

        // Update warmup state for observable gauge callbacks
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        if (_loadsDequeuedCounter != null)
        {
            _loadsDequeuedCounter.Add(1,
                new KeyValuePair<string, object?>("sna.queue.name", _queue.Name),
                new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));

            // Track metric data point for volume estimation
            _volumeEstimator?.RecordMetricDataPoint();
        }

        // Calculate wait time
        if (_enqueueTimes.TryRemove(load, out long enqueueTime))
        {
            if (!_timeUnit.HasValue)
            {
                throw new InvalidOperationException(
                    $"Time unit must be set before recording wait time metrics. " +
                    $"Call SetTimeUnit() during model initialization (e.g., in Initialize(IRunContext context) using context.TimeUnit).");
            }

            // Calculate wait time in simulation time units
            long waitTimeUnits = clockTime - enqueueTime;

            // Convert to seconds using TimeUnitConverter
            double waitSeconds = TimeUnitConverter.ConvertFromSimulationUnits(waitTimeUnits, _timeUnit.Value).TotalSeconds;

            // Emit raw wait time to histogram - backend will calculate averages/percentiles/max
            // This follows the "Emitter not Calculator" principle (plan.md watch-out #4)
            if (_waitTimeHistogram != null)
            {
                _waitTimeHistogram.Record(waitSeconds,
                    new KeyValuePair<string, object?>("sna.queue.name", _queue.Name),
                    new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));

                // Track metric data point for volume estimation
                _volumeEstimator?.RecordMetricDataPoint();
            }
        }
    }

    private void OnLoadBalked(TLoad load, long clockTime)
    {
        _loadsBalked++;
        bool isWarmup = GetWarmupPhase();

        // Update warmup state for observable gauge callbacks
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        if (_loadsBalkedCounter != null)
        {
            _loadsBalkedCounter.Add(1,
                new KeyValuePair<string, object?>("sna.queue.name", _queue.Name),
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
    public static QueueObserver<TLoad> CreateSimple(ISimQueue<TLoad> queue)
    {
        // Create a new meter for this observer to avoid memory leaks from static shared meters
        var meter = new Meter(SimulationTelemetry.MeterName);
        return new QueueObserver<TLoad>(queue, meter, ownsMeter: true);
    }

    public void Dispose()
    {
        // Mark as disposed (thread-safe write)
        // This prevents observable gauge callbacks from executing after disposal
        Interlocked.Exchange(ref _disposedInt, 1);

        _queue.LoadEnqueued -= OnLoadEnqueued;
        _queue.LoadDequeued -= OnLoadDequeued;
        _queue.LoadBalked -= OnLoadBalked;

        // Clear enqueue times tracking
        _enqueueTimes.Clear();

        // Dispose the meter if we own it (prevents memory leak from static shared meters)
        // This disposes all instruments created on the meter, including the observable gauge callbacks
        if (_ownsMeter)
        {
            _meter?.Dispose();
        }
        // Note: When using a shared meter (ownsMeter = false), we can't unregister the observable gauge.
        // The _disposedInt flag ensures the callback returns early and doesn't hold references,
        // allowing the observer to be garbage collected despite the meter's reference.

        GC.SuppressFinalize(this);
    }
}

public static class QueueObserver
{
    // Helper to avoid specifying TLoad for type inference where possible
    public static QueueObserver<TLoad> CreateSimple<TLoad>(ISimQueue<TLoad> queue)
        where TLoad : notnull
    {
        return QueueObserver<TLoad>.CreateSimple(queue);
    }
}
