using System.Text;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.CustomModels;
using System.Diagnostics.Metrics;

namespace SimNextgenApp.Demo.AzureDbSample;

/// <summary>
/// Simulation of Azure Database behavior based on instance specifications.
/// </summary>
internal class AzureDbBehavior(AzureDbInstanceSpec spec, double initialCredits = 10.0)
{
    private IRunContext? _engineContext;

    // Thread-safe storage for cross-thread access by OpenTelemetry's background metric collection
    // Using volatile read/write pattern via Volatile.Read() / Volatile.Write()
    // These are updated on the simulation thread but read by observable gauge callbacks
    private double _credits = initialCredits < 0 ? 0 : initialCredits;

    private long _lastUpdateTimeInSimUnits = 0L;

    // Thread-safe property using volatile read/write pattern
    private double Credits
    {
        get => Volatile.Read(ref _credits);
        set => Volatile.Write(ref _credits, value);
    }

    private readonly BurstableInstanceSpec? _burstableSpec = spec as BurstableInstanceSpec;
    private readonly StringBuilder _latencyBuffer = new("Simulation Time (s),Latency (ms)\n");
    private readonly StringBuilder _creditBuffer = new("Simulation Time (s),Credits\n");

    // OpenTelemetry metrics (optional - enabled via SetMeter)
    private Meter? _meter;
    // ObservableGauge is kept as field to prevent GC - it auto-reports via callback
    private ObservableGauge<double>? _creditsGauge;
    private Histogram<double>? _latencyHistogram;

    // Expose spec for external use (e.g., determining numberOfServers in scenarios)
    public AzureDbInstanceSpec Spec => spec;

    private double MaxCredits => _burstableSpec?.MaxCredits ?? 0;
    private double EarnRatePerSec => (_burstableSpec?.EarnRatePerHour ?? 0) / 3600.0;
    private double BurnRatePerSec => spec.VCores / 60.0;
    private bool IsBurstable => _burstableSpec != null;

    // PostgreSQL connection overhead constants (for PgBouncer-style pooling)
    private const double ConnectionOverheadSecs = 0.050;        // 50ms for new connection setup
    private const double TransactionResetOverheadSecs = 0.008;  // 8ms for DISCARD ALL (state reset)

    public void SetContext(IRunContext context)
    {
        _engineContext = context;
        // Initialize last update time to current simulation time
        _lastUpdateTimeInSimUnits = context.ClockTime;
    }

    /// <summary>
    /// Sets up OpenTelemetry metrics for exporting to Grafana Cloud or other OTLP backends.
    /// </summary>
    public void SetMeter(Meter meter)
    {
        _meter = meter;

        // Create observable gauge for real-time credit tracking with dimensional tags
        _creditsGauge = _meter.CreateObservableGauge(
            "sna.azure_db.cpu_credits",
            () => new Measurement<double>(
                Credits,
                new KeyValuePair<string, object?>("sna.azure_db.instance_series", spec.Series),
                new KeyValuePair<string, object?>("sna.azure_db.instance_size", spec.Size),
                new KeyValuePair<string, object?>("sna.azure_db.is_burstable", IsBurstable)
            ),
            unit: "credits",
            description: "Current CPU credits balance");

        // Create histogram for latency distribution
        _latencyHistogram = _meter.CreateHistogram<double>(
            "sna.azure_db.query_latency",
            unit: "ms",
            description: "Query latency distribution in milliseconds");
    }

    public TimeSpan GetServiceTime(MyLoad load, Random rnd)
    {
        if (_engineContext == null) throw new InvalidOperationException("Context missing");

        // Keep time as long (simulation units) for precision
        long currentTimeInSimUnits = _engineContext.ClockTime;
        long timeDeltaInSimUnits = currentTimeInSimUnits - _lastUpdateTimeInSimUnits;

        // 1. Earn Credits
        if (IsBurstable && timeDeltaInSimUnits > 0)
        {
            // Convert to seconds only for Azure credit calculations
            double timeDeltaSeconds = TimeUnitConverter.ConvertFromSimulationUnits(
                timeDeltaInSimUnits,
                _engineContext.TimeUnit
            ).TotalSeconds;

            double earned = timeDeltaSeconds * EarnRatePerSec;

            // Add earned credits to regular balance (capped at max)
            Credits = Math.Min(MaxCredits, Credits + earned);
            _lastUpdateTimeInSimUnits = currentTimeInSimUnits;
        }

        // 2. Burn Logic (Look Ahead)
        double estimatedBurstCost = spec.FastSecs * BurnRatePerSec;
        bool isThrottled = IsBurstable && Credits < estimatedBurstCost;

        // 3. Determine Service Time (with PostgreSQL connection overhead if applicable)
        double baseTime;
        if (load is PostgresQuery query)
        {
            // Get base execution time (burst or throttled)
            double executionTime = isThrottled ? spec.SlowSecs : spec.FastSecs;

            if (query.IsNewConnection)
            {
                // Full connection setup overhead (direct or pool miss)
                baseTime = executionTime + ConnectionOverheadSecs;
            }
            else if (query.PoolMode == PoolingMode.TransactionPooling)
            {
                // Transaction pooling: connection state reset overhead
                // (DISCARD ALL, temp table cleanup, session var reset)
                baseTime = executionTime + TransactionResetOverheadSecs;
            }
            else
            {
                // Session pooling: no overhead (connection reused as-is)
                baseTime = executionTime;
            }
        }
        else
        {
            // Fallback for non-PostgresQuery loads
            baseTime = isThrottled ? spec.SlowSecs : spec.FastSecs;
        }

        double actualDuration = -baseTime * Math.Log(1.0 - rnd.NextDouble());

        // 4. Pay the Bill
        // Azure: No unlimited mode - hard throttle when credits depleted
        if (IsBurstable)
        {
            double actualBurn = actualDuration * BurnRatePerSec;

            if (Credits >= actualBurn)
            {
                // Normal case: sufficient credits available
                Credits -= actualBurn;
            }
            else
            {
                // Azure: Hard throttle - deplete credits to zero (no debt/surplus)
                Credits = 0;
            }
        }

        // 5. Export Data (CSV)
        // Convert current simulation time to seconds for CSV export
        double nowInSeconds = TimeUnitConverter.ConvertFromSimulationUnits(
            currentTimeInSimUnits,
            _engineContext.TimeUnit
        ).TotalSeconds;
        ExportMetrics(nowInSeconds, actualDuration, isThrottled);

        return TimeSpan.FromSeconds(actualDuration);
    }

    private void ExportMetrics(double now, double actualDuration, bool isThrottled)
    {
        // Export to CSV buffers (always enabled)
        _latencyBuffer.AppendLine($"{now:F2},{actualDuration * 1000:F0}");

        // Export credit data (no surplus debt column for Azure)
        _creditBuffer.AppendLine($"{now:F2},{Credits:F4}");

        // Export to OpenTelemetry histogram (if enabled via SetMeter)
        if (_latencyHistogram != null)
        {
            _latencyHistogram.Record(
                actualDuration * 1000, // Convert to milliseconds
                new KeyValuePair<string, object?>("sna.azure_db.instance_series", spec.Series),
                new KeyValuePair<string, object?>("sna.azure_db.instance_size", spec.Size),
                new KeyValuePair<string, object?>("sna.azure_db.is_throttled", isThrottled),
                new KeyValuePair<string, object?>("sna.azure_db.is_burstable", IsBurstable)
            );
        }
    }

    public void FinalizeExport(string directory)
    {
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string creditsFilePath = Path.Combine(directory, "simulation_credits.csv");
        if (File.Exists(creditsFilePath))
        {
            File.Delete(creditsFilePath);
        }

        if (IsBurstable)
        {
            File.WriteAllText(creditsFilePath, _creditBuffer.ToString());
        }

        File.WriteAllText(Path.Combine(directory, "simulation_latency.csv"), _latencyBuffer.ToString());
    }
}
