using System.Diagnostics;
using System.Diagnostics.Metrics;
using SimNextgenApp.Core;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Observability;

/// <summary>
/// A beginner-friendly convenience API that tracks whole-simulation performance metrics
/// and automatically emits OpenTelemetry measurements underneath.
/// </summary>
/// <remarks>
/// Unlike component-specific observers (ServerObserver, GeneratorObserver, etc.),
/// SimulationObserver tracks simulation-wide metrics such as total events executed,
/// simulation clock time, real time elapsed, and overall simulation performance.
/// </remarks>
public class SimulationObserver : IDisposable
{
    private readonly SimulationEngine _engine;
    private readonly Meter? _meter;
    private readonly bool _ownsMeter;
    private readonly VolumeEstimator? _volumeEstimator;
    private readonly Stopwatch _realTimeStopwatch;
    private readonly long? _warmupEndTime;

    // OpenTelemetry Instruments
    private readonly ObservableCounter<long>? _eventsCounter;
    private readonly ObservableGauge<long>? _clockTimeGauge;
    private readonly ObservableGauge<double>? _realTimeGauge;
    private readonly ObservableGauge<double>? _eventsPerSecondGauge;

    /// <summary>
    /// Gets the underlying OpenTelemetry Meter for advanced use cases.
    /// </summary>
    public Meter? Meter => _meter;

    /// <summary>
    /// Total number of events executed in the simulation.
    /// </summary>
    public long TotalEventsExecuted => _engine.ExecutedEventCount;

    /// <summary>
    /// Current simulation clock time (in simulation time units).
    /// </summary>
    public long SimulationClockTime => _engine.ClockTime;

    /// <summary>
    /// Wall-clock time elapsed since the observer was created (in seconds).
    /// </summary>
    public double ElapsedRealTime => _realTimeStopwatch.Elapsed.TotalSeconds;

    /// <summary>
    /// Simulation performance metric: events processed per second of real time.
    /// </summary>
    public double EventsPerSecond
    {
        get
        {
            var elapsed = _realTimeStopwatch.Elapsed.TotalSeconds;
            if (elapsed <= 0) return 0.0;
            return TotalEventsExecuted / elapsed;
        }
    }

    /// <summary>
    /// Whether the warmup phase has finished.
    /// Returns true if no warmup period is configured.
    /// </summary>
    public bool WarmupComplete
    {
        get
        {
            if (!_warmupEndTime.HasValue) return true;
            return _engine.ClockTime >= _warmupEndTime.Value;
        }
    }

    /// <summary>
    /// Determines if the simulation is currently in warmup phase.
    /// </summary>
    private bool IsWarmupPhase
    {
        get
        {
            if (!_warmupEndTime.HasValue) return false;
            return _engine.ClockTime < _warmupEndTime.Value;
        }
    }

    internal SimulationObserver(
        SimulationEngine engine,
        Meter? meter,
        bool ownsMeter = false,
        VolumeEstimator? volumeEstimator = null,
        long? warmupEndTime = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _meter = meter;
        _ownsMeter = ownsMeter;
        _volumeEstimator = volumeEstimator;
        _warmupEndTime = warmupEndTime;
        _realTimeStopwatch = Stopwatch.StartNew();

        if (_meter != null)
        {
            // Use ObservableCounter to read ExecutedEventCount directly from engine
            // No need for manual tracking - engine already maintains this count
            _eventsCounter = _meter.CreateObservableCounter<long>(
                "sna.simulation.events_total",
                () => new Measurement<long>(
                    TotalEventsExecuted,
                    new KeyValuePair<string, object?>("sna.simulation.warmup", IsWarmupPhase)),
                description: "Total simulation events processed");

            _clockTimeGauge = _meter.CreateObservableGauge<long>(
                "sna.simulation.clock_time",
                () => new Measurement<long>(
                    SimulationClockTime,
                    new KeyValuePair<string, object?>("sna.simulation.warmup", IsWarmupPhase)),
                description: "Current simulation clock time"
            );

            _realTimeGauge = _meter.CreateObservableGauge<double>(
                "sna.simulation.real_time_elapsed",
                () => new Measurement<double>(
                    ElapsedRealTime,
                    new KeyValuePair<string, object?>("sna.simulation.warmup", IsWarmupPhase)),
                unit: "s",
                description: "Wall-clock time elapsed since simulation start"
            );

            _eventsPerSecondGauge = _meter.CreateObservableGauge<double>(
                "sna.simulation.events_per_second",
                () => new Measurement<double>(
                    EventsPerSecond,
                    new KeyValuePair<string, object?>("sna.simulation.warmup", IsWarmupPhase)),
                description: "Simulation performance metric: events processed per second of real time"
            );
        }
    }

    /// <summary>
    /// Creates a simple observer that uses the default OpenTelemetry meter.
    /// This is the primary beginner-friendly entry point.
    /// </summary>
    /// <param name="engine">The simulation engine to observe.</param>
    /// <param name="warmupEndTime">Optional warmup end time (in simulation time units).</param>
    /// <remarks>
    /// The observer creates a dedicated meter instance that is disposed when the observer is disposed,
    /// preventing memory leaks when running multiple simulations.
    /// </remarks>
    public static SimulationObserver CreateSimple(SimulationEngine engine, long? warmupEndTime = null)
    {
        // Create a new meter for this observer to avoid memory leaks from static shared meters
        var meter = new Meter(SimulationTelemetry.MeterName);
        return new SimulationObserver(engine, meter, ownsMeter: true, warmupEndTime: warmupEndTime);
    }

    public void Dispose()
    {
        _realTimeStopwatch.Stop();

        // Dispose the meter if we own it
        // This disposes all instruments created on the meter, including the observable gauge callbacks
        if (_ownsMeter)
        {
            _meter?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
