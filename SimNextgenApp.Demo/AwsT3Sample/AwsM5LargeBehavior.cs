namespace SimNextgenApp.Demo.AwsT3Sample;

/// <summary>
/// Specific behavior for M5.Large RDS instances (Fixed Performance).
/// </summary>
internal class AwsM5LargeBehavior() : AwsRdsBehaviorBase(0) // No initial credits needed
{
    protected override double MaxCredits => 0;
    protected override double EarnRatePerSec => 0;
    protected override double BurnRatePerSec => 0;
    
    // M5 has consistent performance
    protected override double FastServiceSecs => 0.045; // 45ms
    protected override double SlowServiceSecs => 0.045; // No throttling
    
    protected override bool IsBurstable => false;
    protected override bool IsUnlimited => false;
}
