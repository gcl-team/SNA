using System.Text;
using System.IO;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.CustomModels;

namespace SimNextgenApp.Demo.AwsRdsSample;

/// <summary>
/// Simulation of AWS RDS behavior based on instance specifications.
/// </summary>
internal class AwsRdsBehavior(AwsRdsInstanceSpec spec, double initialCredits = 5.0, bool isUnlimited = false)
{
    private IRunContext? _engineContext;
    private double _credits = initialCredits < 0 ? 0 : initialCredits;
    private double _lastUpdateTime = 0.0;
    private double _timeUnitToSecondsMultiplier = 1.0; // Default: assume time units are seconds

    private readonly BurstableInstanceSpec? _burstableSpec = spec as BurstableInstanceSpec;
    private readonly StringBuilder _latencyBuffer = new("Time (s),Latency (ms)\n");
    private readonly StringBuilder _creditBuffer = new("Time (s),Credits\n");

    private double MaxCredits => _burstableSpec?.MaxCredits ?? 0;
    private double EarnRatePerSec => (_burstableSpec?.EarnRatePerHour ?? 0) / 3600.0;
    private double BurnRatePerSec => (spec.VCpus) / 60.0;
    private bool IsBurstable => _burstableSpec != null;

    public void SetContext(IRunContext context, SimulationTimeUnit timeUnit)
    {
        _engineContext = context;

        // Calculate conversion factor from simulation time units to seconds
        _timeUnitToSecondsMultiplier = timeUnit switch
        {
            SimulationTimeUnit.Ticks => 1.0 / TimeSpan.TicksPerSecond,
            SimulationTimeUnit.Microseconds => 1.0 / 1_000_000.0,
            SimulationTimeUnit.Milliseconds => 1.0 / 1_000.0,
            SimulationTimeUnit.Seconds => 1.0,
            SimulationTimeUnit.Minutes => 60.0,
            SimulationTimeUnit.Hours => 3600.0,
            SimulationTimeUnit.Days => 86400.0,
            _ => 1.0
        };
    }

    public TimeSpan GetServiceTime(MyLoad load, Random rnd)
    {
        if (_engineContext == null) throw new InvalidOperationException("Context missing");

        // ClockTime is in simulation units, convert to seconds for credit calculations (AWS credit logic is per-second)
        double now = _engineContext.ClockTime * _timeUnitToSecondsMultiplier;
        double timeDelta = now - _lastUpdateTime;

        // 1. Earn Credits
        if (IsBurstable && timeDelta > 0)
        {
            double earned = timeDelta * EarnRatePerSec;
            _credits = Math.Min(MaxCredits, _credits + earned);
            _lastUpdateTime = now;
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
            _credits = Math.Max(0, _credits - actualBurn);
        }

        // 5. Export Data (CSV)
        ExportMetrics(now, actualDuration);

        return TimeSpan.FromSeconds(actualDuration);
    }

    private void ExportMetrics(double now, double actualDuration)
    {
        _latencyBuffer.AppendLine($"{now:F2},{actualDuration * 1000:F0}");
        _creditBuffer.AppendLine($"{now:F2},{_credits:F4}");
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
