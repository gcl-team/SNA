using SimNextgenApp.Core;
using SimNextgenApp.Demo.CustomModels;

namespace SimNextgenApp.Demo.AwsT3Sample;

/// <summary>
/// Simulation of AWS RDS behavior based on instance specifications.
/// </summary>
internal class AwsRdsBehavior(AwsRdsInstanceSpec spec, double initialCredits = 5.0, bool isUnlimited = false)
{
    private IRunContext? _engineContext;
    private double _credits = initialCredits;
    private double _lastUpdateTime = 0.0;

    private readonly BurstableInstanceSpec? _burstableSpec = spec as BurstableInstanceSpec;

    private double MaxCredits => _burstableSpec?.MaxCredits ?? 0;
    private double EarnRatePerSec => (_burstableSpec?.EarnRatePerHour ?? 0) / 3600.0;
    private double BurnRatePerSec => (spec.VCpus) / 60.0;
    private bool IsBurstable => _burstableSpec != null;

    public void SetContext(IRunContext context)
    {
        _engineContext = context;
    }

    public TimeSpan GetServiceTime(MyLoad load, Random rnd)
    {
        if (_engineContext == null) throw new InvalidOperationException("Context missing");

        double now = _engineContext.ClockTime;
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
        string csvLatencyLine = $"{now:F2},{actualDuration * 1000:F0}";
        File.AppendAllText($"./output/simulation_latency.csv", csvLatencyLine + Environment.NewLine);
        string csvCreditLine = $"{now:F2},{_credits:F4}";
        File.AppendAllText($"./output/simulation_credits.csv", csvCreditLine + Environment.NewLine);
    }
}
