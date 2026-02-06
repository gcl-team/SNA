namespace SimNextgenApp.Demo.AwsT3Sample;

/// <summary>
/// Specific behavior for T4g.Medium RDS instances (Graviton).
/// </summary>
internal class AwsT4gMediumBehavior(double initialCredits = 5.0, bool isUnlimited = true) 
    : AwsRdsBehaviorBase(initialCredits)
{
    protected override double MaxCredits => 576.0;
    protected override double EarnRatePerSec => 24.0 / 3600.0;
    protected override double BurnRatePerSec => 2.0 / 60.0; // 2 vCPUs
    
    // Graviton performance advantage (~20% faster)
    protected override double FastServiceSecs => 0.040; // 40ms
    protected override double SlowServiceSecs => 2.400; // 2.4s
    
    protected override bool IsBurstable => true;
    protected override bool IsUnlimited => isUnlimited;
}
