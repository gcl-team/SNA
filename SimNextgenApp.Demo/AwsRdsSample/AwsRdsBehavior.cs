using System.Text;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.CustomModels;
using System.Diagnostics.Metrics;

namespace SimNextgenApp.Demo.AwsRdsSample;

/// <summary>
/// Simulation of AWS RDS behavior based on instance specifications.
/// </summary>
internal class AwsRdsBehavior(AwsRdsInstanceSpec spec, double initialCredits = 5.0, bool isUnlimited = false)
{
    private IRunContext? _engineContext;

    // Thread-safe storage for cross-thread access by OpenTelemetry's background metric collection
    // Using volatile read/write pattern via Volatile.Read() / Volatile.Write()
    // These are updated on the simulation thread but read by observable gauge callbacks
    private double _credits = initialCredits < 0 ? 0 : initialCredits;
    private double _surplusCreditDebt = 0.0;

    private long _lastUpdateTimeInSimUnits = 0L;

    // Thread-safe properties using volatile read/write pattern
    private double Credits
    {
        get => Volatile.Read(ref _credits);
        set => Volatile.Write(ref _credits, value);
    }

    private double SurplusCreditDebt
    {
        get => Volatile.Read(ref _surplusCreditDebt);
        set => Volatile.Write(ref _surplusCreditDebt, value);
    }

    // AWS fixed pricing for surplus credits
    // private const double AWS_SURPLUS_COST_PER_VCPU_HOUR = 0.05;

    private readonly BurstableInstanceSpec? _burstableSpec = spec as BurstableInstanceSpec;
    private readonly StringBuilder _latencyBuffer = new("Simulation Time (s),Latency (ms)\n");
    private readonly StringBuilder _creditBuffer = new("Simulation Time (s),Credits,Surplus Credit Debt\n");

    // OpenTelemetry metrics (optional - enabled via SetMeter)
    private Meter? _meter;
    // ObservableGauges are kept as fields to prevent GC - they auto-report via callbacks
    private ObservableGauge<double>? _creditsGauge;
    private ObservableGauge<double>? _surplusDebtGauge;
    private Histogram<double>? _latencyHistogram;

    private double MaxCredits => _burstableSpec?.MaxCredits ?? 0;
    private double EarnRatePerSec => (_burstableSpec?.EarnRatePerHour ?? 0) / 3600.0;
    private double BurnRatePerSec => (spec.VCpus) / 60.0;
    private bool IsBurstable => _burstableSpec != null;

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

        // Create observable gauges for real-time credit tracking with dimensional tags
        _creditsGauge = _meter.CreateObservableGauge(
            "sna.rds.cpu_credits",
            () => new Measurement<double>(
                Credits,
                new KeyValuePair<string, object?>("sna.rds.instance_family", spec.Family),
                new KeyValuePair<string, object?>("sna.rds.instance_size", spec.Size),
                new KeyValuePair<string, object?>("sna.rds.is_burstable", IsBurstable)
            ),
            unit: "credits",
            description: "Current CPU credits balance");

        _surplusDebtGauge = _meter.CreateObservableGauge(
            "sna.rds.surplus_credit_debt",
            () => new Measurement<double>(
                SurplusCreditDebt,
                new KeyValuePair<string, object?>("sna.rds.instance_family", spec.Family),
                new KeyValuePair<string, object?>("sna.rds.instance_size", spec.Size),
                new KeyValuePair<string, object?>("sna.rds.is_burstable", IsBurstable)
            ),
            unit: "credits",
            description: "Surplus credit debt accrued in unlimited mode");

        // Create histogram for latency distribution
        _latencyHistogram = _meter.CreateHistogram<double>(
            "sna.rds.query_latency",
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
            // Convert to seconds only for AWS credit calculations
            double timeDeltaSeconds = TimeUnitConverter.ConvertFromSimulationUnits(
                timeDeltaInSimUnits,
                _engineContext.TimeUnit
            ).TotalSeconds;

            double earned = timeDeltaSeconds * EarnRatePerSec;

            // First repay surplus credits (debt) if in unlimited mode
            if (isUnlimited && SurplusCreditDebt > 0)
            {
                double surplusRepayment = Math.Min(SurplusCreditDebt, earned);
                SurplusCreditDebt -= surplusRepayment;
                earned -= surplusRepayment;
            }

            // Then add remaining earned credits to regular balance (capped at max)
            Credits = Math.Min(MaxCredits, Credits + earned);
            _lastUpdateTimeInSimUnits = currentTimeInSimUnits;
        }

        // 2. Burn Logic (Look Ahead)
        double estimatedBurstCost = spec.FastSecs * BurnRatePerSec;
        bool isThrottled = IsBurstable && Credits < estimatedBurstCost;

        // 3. Determine Service Time
        double baseTime = isThrottled && !isUnlimited ? spec.SlowSecs : spec.FastSecs;
        double actualDuration = -baseTime * Math.Log(1.0 - rnd.NextDouble());

        // 4. Pay the Bill
        if (IsBurstable)
        {
            double actualBurn = actualDuration * BurnRatePerSec;

            if (Credits >= actualBurn)
            {
                // Normal case: sufficient credits available
                Credits -= actualBurn;
            }
            else if (isUnlimited)
            {
                // Unlimited mode: accrue surplus credits (debt)
                double shortage = actualBurn - Credits;
                Credits = 0;
                SurplusCreditDebt += shortage;
            }
            else
            {
                // Standard mode: deplete credits to zero (already throttled)
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

        // Export credit data (Credits and Surplus Credits share the same unit for easy plotting)
        _creditBuffer.AppendLine($"{now:F2},{Credits:F4},{SurplusCreditDebt:F4}");

        // Export to OpenTelemetry histogram (if enabled via SetMeter)
        if (_latencyHistogram != null)
        {
            _latencyHistogram.Record(
                actualDuration * 1000, // Convert to milliseconds
                new KeyValuePair<string, object?>("sna.rds.instance_family", spec.Family),
                new KeyValuePair<string, object?>("sna.rds.instance_size", spec.Size),
                new KeyValuePair<string, object?>("sna.rds.is_throttled", isThrottled),
                new KeyValuePair<string, object?>("sna.rds.is_burstable", IsBurstable)
            );
        }
    }

    public void FinalizeExport(string directory)
    {
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string creditsFilePath = Path.Combine(directory, "simulationCredits.csv");
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
