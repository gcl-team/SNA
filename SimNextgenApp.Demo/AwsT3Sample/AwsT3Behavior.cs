using SimNextgenApp.Core;
using SimNextgenApp.Demo.CustomModels;

namespace SimNextgenApp.Demo.AwsT3Sample;

/// <summary>
/// Behavior configuration for Amazon Database T3 instances.
/// </summary>
internal class AwsT3Behavior(double initialCredits = 5.0, bool isUnlimitedCredits = false)
{
    private IRunContext? _engineContext;
    
    private double _credits = initialCredits;

    private bool _isUnlimitedCredits = isUnlimitedCredits;
    
    private readonly double _maxCredits = 576.0; // T3.Medium Specs
    
    private readonly double _earnRatePerSec = 24.0 / 3600.0; // Earns 24 credits per hour
    
    // Burns 1 credit per vCPU-minute. T3.Medium has 2 vCPUs.
    // At 100% load, it burns 2 credits/minute.
    private readonly double _burnRatePerSec = 2.0 / 60.0; 
    
    private double _lastUpdateTime = 0.0;

    // Latencies
    private readonly double _fastServiceSecs = 0.050; // 50ms
    private readonly double _slowServiceSecs = 3.000; // 3s (Painful)

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
        if (timeDelta > 0)
        {
            double earned = timeDelta * _earnRatePerSec;
            _credits = Math.Min(_maxCredits, _credits + earned);
            _lastUpdateTime = now;
        }

        // 2. Burn Logic (Look Ahead)
        // We estimate the cost of a "Fast" request to see if we can afford it.
        // Calculate the cost of a standard "Burst" request
        double estimatedBurstCost = _fastServiceSecs * _burnRatePerSec;

        // We are "Empty" if we cannot afford even ONE single burst request.
        // This prevents the "Micro-Burst" loop.
        bool isThrottled = _credits < estimatedBurstCost; 

        // 3. Determine Service Time
        double baseTime = isThrottled && !_isUnlimitedCredits ? _slowServiceSecs : _fastServiceSecs;
        double actualDuration = -baseTime * Math.Log(1.0 - rnd.NextDouble());

        // 4. Pay the Bill
        // The longer the request took, the more credits we burned.
        double actualBurn = actualDuration * _burnRatePerSec;
        _credits = Math.Max(0, _credits - actualBurn);

        // 5. Export Data (CSV)
        // Adjust the time multiplier if you want to align X-Axis visually
        string csvLatencyLine = $"{now:F2},{actualDuration * 1000:F0}";
        File.AppendAllText($"simulation_latency.csv", csvLatencyLine + Environment.NewLine);
        string csvCreditLine = $"{now:F2},{_credits:F4}";
        File.AppendAllText($"simulation_credits.csv", csvCreditLine + Environment.NewLine);

        return TimeSpan.FromSeconds(actualDuration);
    }
}