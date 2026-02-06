namespace SimNextgenApp.Demo.AwsT3Sample;

/// <summary>
/// Specific behavior for T3.Medium RDS instances.
/// </summary>
internal class AwsT3MediumBehavior(double initialCredits = 5.0, bool isUnlimited = false) 
    : AwsRdsBehaviorBase(initialCredits)
{
    protected override double MaxCredits => 576.0;
    protected override double EarnRatePerSec => 24.0 / 3600.0;
    protected override double BurnRatePerSec => 2.0 / 60.0; // 2 vCPUs
    protected override double FastServiceSecs => 0.050; // 50ms
    protected override double SlowServiceSecs => 3.000; // 3s
    protected override bool IsBurstable => true;
    protected override bool IsUnlimited => isUnlimited;
}
