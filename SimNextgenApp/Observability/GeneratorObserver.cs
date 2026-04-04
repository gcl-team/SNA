using System.Diagnostics;
using System.Diagnostics.Metrics;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Observability;

/// <summary>
/// A beginner-friendly convenience API that tracks a generator's performance metrics
/// and automatically emits OpenTelemetry measurements underneath.
/// </summary>
/// <typeparam name="TLoad">The type of load being generated.</typeparam>
public class GeneratorObserver<TLoad> : IDisposable where TLoad : notnull
{
    private readonly IGenerator<TLoad> _generator;
    private readonly Meter? _meter;
    private readonly bool _ownsMeter;
    private readonly VolumeEstimator? _volumeEstimator;

    // Local lightweight scalar counters for convenience API
    private int _loadsGenerated;
    private long _lastGenerationTime;
    private long _previousGenerationTime;

    // Track warmup state explicitly for observable gauge callbacks.
    // Stored as int (0=false, 1=true) to use Interlocked operations for thread-safe cross-thread visibility.
    // Written by simulation thread, read by ObservableGauge callback thread.
    private int _isWarmupPhaseInt = 1; // 1 = true initially

    // OpenTelemetry Instruments
    private readonly Counter<int>? _loadsGeneratedCounter;
    private readonly Histogram<double>? _interArrivalTimeHistogram;

    /// <summary>
    /// Gets the underlying OpenTelemetry Meter for advanced use cases.
    /// </summary>
    public Meter? Meter => _meter;

    /// <summary>
    /// Total number of loads generated.
    /// </summary>
    public int LoadsGenerated => _loadsGenerated;

    /// <summary>
    /// Time between the last two generated loads (in simulation time units).
    /// Returns null if fewer than 2 loads have been generated.
    /// </summary>
    public long? LastInterArrivalTime =>
        _loadsGenerated >= 2 ? _lastGenerationTime - _previousGenerationTime : null;

    internal GeneratorObserver(IGenerator<TLoad> generator, Meter? meter, bool ownsMeter = false, VolumeEstimator? volumeEstimator = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _meter = meter;
        _ownsMeter = ownsMeter;
        _volumeEstimator = volumeEstimator;

        if (_meter != null)
        {
            _loadsGeneratedCounter = _meter.CreateCounter<int>("sna.generator.loads_generated", description: "Total loads created by the generator");
            _interArrivalTimeHistogram = _meter.CreateHistogram<double>("sna.generator.inter_arrival_time", unit: "ticks", description: "Time between successive load generations");
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
        _generator.LoadGenerated += OnLoadGenerated;
    }

    private void OnLoadGenerated(TLoad load, long clockTime)
    {
        _loadsGenerated++;
        bool isWarmup = GetWarmupPhase();

        // Update warmup state for observable gauge callbacks (which run on background threads)
        // Use Interlocked for thread-safe write with memory barrier guarantees
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        // Track inter-arrival time
        if (_loadsGenerated >= 2)
        {
            long interArrivalTime = clockTime - _lastGenerationTime;

            if (_interArrivalTimeHistogram != null)
            {
                _interArrivalTimeHistogram.Record(interArrivalTime,
                    new KeyValuePair<string, object?>("sna.generator.name", _generator.Name),
                    new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));

                // Track metric data point for volume estimation
                _volumeEstimator?.RecordMetricDataPoint();
            }
        }

        // Update generation times for next iteration
        _previousGenerationTime = _lastGenerationTime;
        _lastGenerationTime = clockTime;

        // Record loads generated counter
        if (_loadsGeneratedCounter != null)
        {
            _loadsGeneratedCounter.Add(1,
                new KeyValuePair<string, object?>("sna.generator.name", _generator.Name),
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
    public static GeneratorObserver<TLoad> CreateSimple(IGenerator<TLoad> generator)
    {
        // Create a new meter for this observer to avoid memory leaks from static shared meters
        var meter = new Meter(SimulationTelemetry.MeterName);
        return new GeneratorObserver<TLoad>(generator, meter, ownsMeter: true);
    }

    public void Dispose()
    {
        _generator.LoadGenerated -= OnLoadGenerated;

        // Dispose the meter if we own it
        // This disposes all instruments created on the meter, including the observable gauge callback
        if (_ownsMeter)
        {
            _meter?.Dispose();
        }
    }
}

public static class GeneratorObserver
{
    // Helper to avoid specifying TLoad for type inference where possible, or factory methods
    public static GeneratorObserver<TLoad> CreateSimple<TLoad>(IGenerator<TLoad> generator) where TLoad : notnull
    {
        return GeneratorObserver<TLoad>.CreateSimple(generator);
    }
}
