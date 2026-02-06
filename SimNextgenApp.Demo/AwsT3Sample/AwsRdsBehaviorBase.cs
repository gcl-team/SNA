using SimNextgenApp.Core;
using SimNextgenApp.Demo.CustomModels;

namespace SimNextgenApp.Demo.AwsT3Sample;

/// <summary>
/// Base class for AWS RDS behavior simulation.
/// </summary>
internal abstract class AwsRdsBehaviorBase(double initialCredits)
{
    protected IRunContext? EngineContext;
    protected double Credits = initialCredits;
    protected double LastUpdateTime = 0.0;

    protected abstract double MaxCredits { get; }
    protected abstract double EarnRatePerSec { get; }
    protected abstract double BurnRatePerSec { get; }
    protected abstract double FastServiceSecs { get; }
    protected abstract double SlowServiceSecs { get; }
    protected abstract bool IsBurstable { get; }
    protected abstract bool IsUnlimited { get; }

    public void SetContext(IRunContext context)
    {
        EngineContext = context;
    }

    public virtual TimeSpan GetServiceTime(MyLoad load, Random rnd)
    {
        if (EngineContext == null) throw new InvalidOperationException("Context missing");

        double now = EngineContext.ClockTime;
        double timeDelta = now - LastUpdateTime;

        // 1. Earn Credits
        if (IsBurstable && timeDelta > 0)
        {
            double earned = timeDelta * EarnRatePerSec;
            Credits = Math.Min(MaxCredits, Credits + earned);
            LastUpdateTime = now;
        }

        // 2. Burn Logic (Look Ahead)
        double estimatedBurstCost = FastServiceSecs * BurnRatePerSec;
        bool isThrottled = IsBurstable && Credits < estimatedBurstCost; 

        // 3. Determine Service Time
        double baseTime = isThrottled && !IsUnlimited ? SlowServiceSecs : FastServiceSecs;
        double actualDuration = -baseTime * Math.Log(1.0 - rnd.NextDouble());

        // 4. Pay the Bill
        if (IsBurstable)
        {
            double actualBurn = actualDuration * BurnRatePerSec;
            Credits = Math.Max(0, Credits - actualBurn);
        }

        // 5. Export Data (CSV) - Optional: could be moved to a protected virtual method
        ExportMetrics(now, actualDuration);

        return TimeSpan.FromSeconds(actualDuration);
    }

    protected virtual void ExportMetrics(double now, double actualDuration)
    {
        // Default implementation matching original behavior
        string csvLatencyLine = $"{now:F2},{actualDuration * 1000:F0}";
        File.AppendAllText($"./output/simulation_latency.csv", csvLatencyLine + Environment.NewLine);
        string csvCreditLine = $"{now:F2},{Credits:F4}";
        File.AppendAllText($"./output/simulation_credits.csv", csvCreditLine + Environment.NewLine);
    }
}
