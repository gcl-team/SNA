namespace SimNextgenApp.Observability.VolumeEstimation;

/// <summary>
/// Tracks telemetry volume to help prevent cost anomalies in observability backends.
/// Monitors spans/sec, metrics/sec, and data points/sec without hardcoding vendor-specific pricing.
/// </summary>
public class VolumeEstimator : IDisposable
{
    private readonly Lock _lock = new();
    private readonly VolumeThresholds _thresholds;
    private readonly TimeProvider _timeProvider;

    // Volume tracking
    private long _totalSpans;
    private long _totalMetricDataPoints;
    private DateTimeOffset _trackingStartTime;

    // Warning state
    private bool _hasWarnedSpans;
    private bool _hasWarnedMetrics;

    /// <summary>
    /// Gets the total number of spans recorded since tracking started.
    /// </summary>
    public long TotalSpans
    {
        get { lock (_lock) return _totalSpans; }
    }

    /// <summary>
    /// Gets the total number of metric data points recorded since tracking started.
    /// </summary>
    public long TotalMetricDataPoints
    {
        get { lock (_lock) return _totalMetricDataPoints; }
    }

    /// <summary>
    /// Gets the current rate of spans per second (based on the measurement window).
    /// </summary>
    public double SpansPerSecond
    {
        get
        {
            lock (_lock)
            {
                var elapsed = _timeProvider.GetUtcNow() - _trackingStartTime;
                if (elapsed.TotalSeconds < 0.1) return 0;
                return _totalSpans / elapsed.TotalSeconds;
            }
        }
    }

    /// <summary>
    /// Gets the current rate of metric data points per second (based on the measurement window).
    /// </summary>
    public double MetricDataPointsPerSecond
    {
        get
        {
            lock (_lock)
            {
                var elapsed = _timeProvider.GetUtcNow() - _trackingStartTime;
                if (elapsed.TotalSeconds < 0.1) return 0;
                return _totalMetricDataPoints / elapsed.TotalSeconds;
            }
        }
    }

    /// <summary>
    /// Gets the elapsed time since tracking started.
    /// </summary>
    public TimeSpan ElapsedTime
    {
        get
        {
            lock (_lock)
            {
                return _timeProvider.GetUtcNow() - _trackingStartTime;
            }
        }
    }

    /// <summary>
    /// Occurs when telemetry volume exceeds the configured thresholds.
    /// </summary>
    public event EventHandler<VolumeWarningEventArgs>? VolumeWarning;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeEstimator"/> class with the specified thresholds.
    /// </summary>
    /// <param name="thresholds">Volume thresholds for triggering warnings.</param>
    /// <param name="timeProvider">Time provider for tracking elapsed time. If null, uses system time.</param>
    public VolumeEstimator(VolumeThresholds thresholds, TimeProvider? timeProvider = null)
    {
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _trackingStartTime = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Records a span being created.
    /// </summary>
    public void RecordSpan()
    {
        VolumeWarningEventArgs? warningToRaise;

        lock (_lock)
        {
            _totalSpans++;
            warningToRaise = CheckSpanThreshold();
        }

        if (warningToRaise != null)
        {
            OnVolumeWarning(warningToRaise);
        }
    }

    /// <summary>
    /// Records multiple spans being created.
    /// </summary>
    /// <param name="count">Number of spans to record.</param>
    public void RecordSpans(int count)
    {
        if (count <= 0) return;

        VolumeWarningEventArgs? warningToRaise;

        lock (_lock)
        {
            _totalSpans += count;
            warningToRaise = CheckSpanThreshold();
        }

        if (warningToRaise != null)
        {
            OnVolumeWarning(warningToRaise);
        }
    }

    /// <summary>
    /// Records a metric data point being created.
    /// </summary>
    public void RecordMetricDataPoint()
    {
        VolumeWarningEventArgs? warningToRaise;

        lock (_lock)
        {
            _totalMetricDataPoints++;
            warningToRaise = CheckMetricThreshold();
        }

        if (warningToRaise != null)
        {
            OnVolumeWarning(warningToRaise);
        }
    }

    /// <summary>
    /// Records multiple metric data points being created.
    /// </summary>
    /// <param name="count">Number of metric data points to record.</param>
    public void RecordMetricDataPoints(int count)
    {
        if (count <= 0) return;

        VolumeWarningEventArgs? warningToRaise;

        lock (_lock)
        {
            _totalMetricDataPoints += count;
            warningToRaise = CheckMetricThreshold();
        }

        if (warningToRaise != null)
        {
            OnVolumeWarning(warningToRaise);
        }
    }

    private VolumeWarningEventArgs? CheckSpanThreshold()
    {
        // Already warned, don't spam
        if (_hasWarnedSpans) return null;

        // Calculate rate directly within lock to avoid nested locking
        var elapsed = _timeProvider.GetUtcNow() - _trackingStartTime;
        if (elapsed.TotalSeconds < 0.1) return null;

        var rate = _totalSpans / elapsed.TotalSeconds;
        if (rate > _thresholds.SpansPerSecondThreshold)
        {
            _hasWarnedSpans = true;
            return new VolumeWarningEventArgs(
                VolumeWarningType.SpanRate,
                rate,
                _thresholds.SpansPerSecondThreshold,
                $"Span rate ({rate:F0}/sec) exceeds threshold ({_thresholds.SpansPerSecondThreshold:F0}/sec). " +
                $"Consider enabling sampling to reduce backend costs.");
        }

        return null;
    }

    private VolumeWarningEventArgs? CheckMetricThreshold()
    {
        // Already warned, don't spam
        if (_hasWarnedMetrics) return null;

        // Calculate rate directly within lock to avoid nested locking
        var elapsed = _timeProvider.GetUtcNow() - _trackingStartTime;
        if (elapsed.TotalSeconds < 0.1) return null;

        var rate = _totalMetricDataPoints / elapsed.TotalSeconds;
        if (rate > _thresholds.MetricDataPointsPerSecondThreshold)
        {
            _hasWarnedMetrics = true;
            return new VolumeWarningEventArgs(
                VolumeWarningType.MetricRate,
                rate,
                _thresholds.MetricDataPointsPerSecondThreshold,
                $"Metric data point rate ({rate:F0}/sec) exceeds threshold ({_thresholds.MetricDataPointsPerSecondThreshold:F0}/sec). " +
                $"Consider reducing metric collection frequency or cardinality.");
        }

        return null;
    }

    /// <summary>
    /// Resets all volume counters and warning states.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _totalSpans = 0;
            _totalMetricDataPoints = 0;
            _trackingStartTime = _timeProvider.GetUtcNow();
            _hasWarnedSpans = false;
            _hasWarnedMetrics = false;
        }
    }

    /// <summary>
    /// Gets a summary of the current volume statistics.
    /// </summary>
    public VolumeStatistics GetStatistics()
    {
        lock (_lock)
        {
            // Calculate rates directly within single lock scope to avoid nested locking
            var elapsed = _timeProvider.GetUtcNow() - _trackingStartTime;
            var spansPerSecond = elapsed.TotalSeconds < 0.1 ? 0 : _totalSpans / elapsed.TotalSeconds;
            var metricsPerSecond = elapsed.TotalSeconds < 0.1 ? 0 : _totalMetricDataPoints / elapsed.TotalSeconds;

            return new VolumeStatistics(
                _totalSpans,
                _totalMetricDataPoints,
                spansPerSecond,
                metricsPerSecond,
                elapsed);
        }
    }

    protected virtual void OnVolumeWarning(VolumeWarningEventArgs e)
    {
        VolumeWarning?.Invoke(this, e);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Defines volume thresholds for triggering warnings.
/// </summary>
public class VolumeThresholds
{
    /// <summary>
    /// Gets the threshold for spans per second before triggering a warning.
    /// </summary>
    public double SpansPerSecondThreshold { get; }

    /// <summary>
    /// Gets the threshold for metric data points per second before triggering a warning.
    /// </summary>
    public double MetricDataPointsPerSecondThreshold { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeThresholds"/> class.
    /// </summary>
    /// <param name="spansPerSecond">Threshold for spans per second (default: 10,000).</param>
    /// <param name="metricDataPointsPerSecond">Threshold for metric data points per second (default: 50,000).</param>
    public VolumeThresholds(double spansPerSecond = 10_000, double metricDataPointsPerSecond = 50_000)
    {
        if (spansPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(spansPerSecond), "Threshold must be positive.");
        if (metricDataPointsPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(metricDataPointsPerSecond), "Threshold must be positive.");

        SpansPerSecondThreshold = spansPerSecond;
        MetricDataPointsPerSecondThreshold = metricDataPointsPerSecond;
    }

    /// <summary>
    /// Creates default thresholds suitable for most use cases.
    /// </summary>
    public static VolumeThresholds Default() => new();

    /// <summary>
    /// Creates conservative thresholds for cost-sensitive environments.
    /// </summary>
    public static VolumeThresholds Conservative() => new(spansPerSecond: 1_000, metricDataPointsPerSecond: 5_000);

    /// <summary>
    /// Creates permissive thresholds for high-volume environments.
    /// </summary>
    public static VolumeThresholds Permissive() => new(spansPerSecond: 100_000, metricDataPointsPerSecond: 500_000);
}

/// <summary>
/// Represents volume statistics at a point in time.
/// </summary>
public record VolumeStatistics(
    long TotalSpans,
    long TotalMetricDataPoints,
    double SpansPerSecond,
    double MetricDataPointsPerSecond,
    TimeSpan ElapsedTime);

/// <summary>
/// Type of volume warning.
/// </summary>
public enum VolumeWarningType
{
    /// <summary>
    /// Span rate has exceeded the threshold.
    /// </summary>
    SpanRate,

    /// <summary>
    /// Metric data point rate has exceeded the threshold.
    /// </summary>
    MetricRate
}

/// <summary>
/// Event arguments for volume warning events.
/// </summary>
public class VolumeWarningEventArgs : EventArgs
{
    /// <summary>
    /// Gets the type of volume warning.
    /// </summary>
    public VolumeWarningType WarningType { get; }

    /// <summary>
    /// Gets the current rate that triggered the warning.
    /// </summary>
    public double CurrentRate { get; }

    /// <summary>
    /// Gets the threshold that was exceeded.
    /// </summary>
    public double Threshold { get; }

    /// <summary>
    /// Gets a human-readable message describing the warning.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeWarningEventArgs"/> class.
    /// </summary>
    public VolumeWarningEventArgs(VolumeWarningType warningType, double currentRate, double threshold, string message)
    {
        WarningType = warningType;
        CurrentRate = currentRate;
        Threshold = threshold;
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }
}
