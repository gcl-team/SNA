using System.Diagnostics;
using System.Diagnostics.Metrics;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Modeling.Server;

namespace SimNextgenApp.Observability;

/// <summary>
/// A beginner-friendly convenience API that tracks a server's performance metrics 
/// and automatically emits OpenTelemetry measurements underneath.
/// </summary>
/// <typeparam name="TLoad">The type of load being processed by the server.</typeparam>
public class SimulationObserver<TLoad> : IDisposable
{
    private static readonly Meter SharedMeter = new(SimulationTelemetry.MeterName);

    private readonly IServer<TLoad> _server;
    private readonly Meter? _meter;
    private SimulationTimeUnit? _timeUnit;

    // Local lightweight scalar counters for convenience API
    private int _loadsCompleted;

    // Track warmup state explicitly for observable gauge callbacks.
    // Stored as int (0=false, 1=true) to use Interlocked operations for thread-safe cross-thread visibility.
    // Written by simulation thread, read by ObservableGauge callback thread.
    private int _isWarmupPhaseInt = 1; // 1 = true initially

    // OpenTelemetry Instruments
    private readonly Counter<int>? _loadsCompletedCounter;
    private readonly Histogram<double>? _sojournTimeHistogram;
    // OTel observable gauges hold no local state and are polled by the backend
    private ObservableGauge<double>? _utilizationGauge;

    /// <summary>
    /// Gets the underlying OpenTelemetry Meter for advanced use cases.
    /// </summary>
    public Meter? Meter => _meter;

    /// <summary>
    /// Total number of loads processed by the server.
    /// </summary>
    public int LoadsCompleted => _loadsCompleted;

    /// <summary>
    /// Instantaneous utilization of the server (ratio of loads in service to capacity).
    /// (0.0 to 1.0)
    /// </summary>
    public double Utilization
    {
        get
        {
            if (_server.Capacity == 0) return 0.0;
            return _server.NumberInService / (double)_server.Capacity;
        }
    }

    /// <summary>
    /// Sets the simulation time unit for proper sojourn time conversion.
    /// Should be called during model initialization when the time unit is known.
    /// </summary>
    public void SetTimeUnit(SimulationTimeUnit timeUnit)
    {
        _timeUnit = timeUnit;
    }

    private SimulationObserver(IServer<TLoad> server, Meter? meter)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _meter = meter;

        if (_meter != null)
        {
            _loadsCompletedCounter = _meter.CreateCounter<int>("sna.server.loads_completed", description: "Total loads completed by the server");
            _sojournTimeHistogram = _meter.CreateHistogram<double>("sna.server.sojourn_time", unit: "s", description: "Time spent by an entity in the server");
            
            _utilizationGauge = _meter.CreateObservableGauge<double>(
                "sna.server.utilization",
                () =>
                {
                    // Thread-safe read of warmup state using Interlocked
                    bool isWarmup = Interlocked.CompareExchange(ref _isWarmupPhaseInt, 0, 0) != 0;

                    return new Measurement<double>(
                        this.Utilization,
                        new KeyValuePair<string, object?>("sna.server.name", _server.Name),
                        new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));
                },
                description: "Instantaneous utilization of the server"
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
        _server.LoadDeparted += OnLoadDeparted;
    }

    private void OnLoadDeparted(TLoad load, long clockTime)
    {
        _loadsCompleted++;
        bool isWarmup = GetWarmupPhase();

        // Update warmup state for observable gauge callbacks (which run on background threads)
        // Use Interlocked for thread-safe write with memory barrier guarantees
        Interlocked.Exchange(ref _isWarmupPhaseInt, isWarmup ? 1 : 0);

        // Record metrics with warmup context from the current Activity span

        if (_loadsCompletedCounter != null)
        {
            _loadsCompletedCounter.Add(1,
                new KeyValuePair<string, object?>("sna.server.name", _server.Name),
                new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));
        }

        // Record sojourn time (time spent in server from entry to departure)
        if (_sojournTimeHistogram != null)
        {
            var serviceStartTime = _server.GetServiceStartTime(load);
            if (serviceStartTime.HasValue)
            {
                if (!_timeUnit.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Time unit must be set before recording sojourn time metrics. " +
                        $"Call SetTimeUnit() during model initialization (e.g., in Initialize(IRunContext context) using context.TimeUnit).");
                }

                // Calculate sojourn time in simulation time units
                long sojournTimeUnits = clockTime - serviceStartTime.Value;

                // Convert to seconds using TimeUnitConverter
                double sojournSeconds = TimeUnitConverter.ConvertFromSimulationUnits(sojournTimeUnits, _timeUnit.Value).TotalSeconds;

                _sojournTimeHistogram.Record(sojournSeconds,
                    new KeyValuePair<string, object?>("sna.server.name", _server.Name),
                    new KeyValuePair<string, object?>("sna.simulation.warmup", isWarmup));
            }
        }
    }

    /// <summary>
    /// Creates a simple observer that relies on the default OpenTelemetry configuration.
    /// This is the primary beginner-friendly entry point.
    /// </summary>
    public static SimulationObserver<TLoad> CreateSimple(IServer<TLoad> server)
    {
        // Reuse the shared meter instance to avoid resource accumulation
        return new SimulationObserver<TLoad>(server, SharedMeter);
    }

    public void Dispose()
    {
        _server.LoadDeparted -= OnLoadDeparted;
    }
}

public static class SimulationObserver
{
    // Helper to avoid specifying TLoad for type inference where possible, or factory methods
    public static SimulationObserver<TLoad> CreateSimple<TLoad>(IServer<TLoad> server)
    {
        return SimulationObserver<TLoad>.CreateSimple(server);
    }
}
