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
    private double _credits = initialCredits < 0 ? 0 : initialCredits;
    private double _surplusCreditDebt = 0.0; // Outstanding debt balance accrued when unlimited mode is enabled (can be repaid)
    private long _lastUpdateTimeInSimUnits = 0L;

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
            "rds.cpu_credits",
            () => new Measurement<double>(
                _credits,
                new KeyValuePair<string, object?>("sna.rds.instance_family", spec.Family),
                new KeyValuePair<string, object?>("sna.rds.instance_size", spec.Size),
                new KeyValuePair<string, object?>("sna.rds.is_burstable", IsBurstable)
            ),
            unit: "credits",
            description: "Current CPU credits balance");

        _surplusDebtGauge = _meter.CreateObservableGauge(
            "rds.surplus_credit_debt",
            () => new Measurement<double>(
                _surplusCreditDebt,
                new KeyValuePair<string, object?>("sna.rds.instance_family", spec.Family),
                new KeyValuePair<string, object?>("sna.rds.instance_size", spec.Size),
                new KeyValuePair<string, object?>("sna.rds.is_burstable", IsBurstable)
            ),
            unit: "credits",
            description: "Surplus credit debt accrued in unlimited mode");

        // Create histogram for latency distribution
        _latencyHistogram = _meter.CreateHistogram<double>(
            "rds.query_latency",
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
            if (isUnlimited && _surplusCreditDebt > 0)
            {
                double surplusRepayment = Math.Min(_surplusCreditDebt, earned);
                _surplusCreditDebt -= surplusRepayment;
                earned -= surplusRepayment;
            }

            // Then add remaining earned credits to regular balance (capped at max)
            _credits = Math.Min(MaxCredits, _credits + earned);
            _lastUpdateTimeInSimUnits = currentTimeInSimUnits;
        }

        // 2. Burn Logic (Look Ahead)
        double estimatedBurstCost = spec.FastSecs * BurnRatePerSec;
        bool isThrottled = IsBurstable && _credits < estimatedBurstCost;

        // 3. Determine Service Time
        double baseTime = isThrottled && !isUnlimited ? spec.SlowSecs : spec.FastSecs;
        double actualDuration = -baseTime * Math.Log(1.0 - rnd.NextDouble());

        // 4. Pay the Bill
        if (IsBurstable)
        {
            double actualBurn = actualDuration * BurnRatePerSec;

            if (_credits >= actualBurn)
            {
                // Normal case: sufficient credits available
                _credits -= actualBurn;
            }
            else if (isUnlimited)
            {
                // Unlimited mode: accrue surplus credits (debt)
                double shortage = actualBurn - _credits;
                _credits = 0;
                _surplusCreditDebt += shortage;
            }
            else
            {
                // Standard mode: deplete credits to zero (already throttled)
                _credits = 0;
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
        _creditBuffer.AppendLine($"{now:F2},{_credits:F4},{_surplusCreditDebt:F4}");

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
