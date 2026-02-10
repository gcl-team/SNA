namespace SimNextgenApp.Demo.AwsRdsSample;

/// <summary>
/// Base record for AWS RDS instance specifications.
/// </summary>
internal abstract record AwsRdsInstanceSpec(
    string Family,
    string Size,
    int VCpus,
    double FastSecs,
    double SlowSecs
);

/// <summary>
/// Specification for burstable (T-series) instances.
/// </summary>
internal record BurstableInstanceSpec(
    string Family,
    string Size,
    int VCpus,
    double FastSecs,
    double SlowSecs,
    double EarnRatePerHour,
    double MaxCredits
) : AwsRdsInstanceSpec(Family, Size, VCpus, FastSecs, SlowSecs);

/// <summary>
/// Specification for fixed-performance (M, R, C series) instances.
/// </summary>
internal record FixedInstanceSpec(
    string Family,
    string Size,
    int VCpus,
    double FastSecs
) : AwsRdsInstanceSpec(Family, Size, VCpus, FastSecs, FastSecs);

/// <summary>
/// Registry of supported RDS instance specifications.
/// </summary>
internal static class AwsRdsRegistry
{
    private static readonly List<AwsRdsInstanceSpec> Specs = new()
    {
        // T3 - Burstable Intel
        new BurstableInstanceSpec("t3", "micro",  2, 0.050, 3.0, 6,  144),
        new BurstableInstanceSpec("t3", "small",  2, 0.050, 3.0, 12, 288),
        new BurstableInstanceSpec("t3", "medium", 2, 0.050, 3.0, 24, 576),
        new BurstableInstanceSpec("t3", "large",  2, 0.050, 3.0, 36, 864),
        
        // T4g - Burstable Graviton (Faster)
        new BurstableInstanceSpec("t4g", "micro",  2, 0.040, 2.4, 6,  144),
        new BurstableInstanceSpec("t4g", "small",  2, 0.040, 2.4, 12, 288),
        new BurstableInstanceSpec("t4g", "medium", 2, 0.040, 2.4, 24, 576),
        new BurstableInstanceSpec("t4g", "large",  2, 0.040, 2.4, 36, 864),

        // M5 - Fixed Performance Intel
        new FixedInstanceSpec("m5", "large", 2, 0.045),
        new FixedInstanceSpec("m5", "xlarge", 4, 0.045)
    };

    public static AwsRdsInstanceSpec GetSpec(string family, string size)
    {
        return Specs.FirstOrDefault(s => 
            s.Family.Equals(family, StringComparison.OrdinalIgnoreCase) && 
            s.Size.Equals(size, StringComparison.OrdinalIgnoreCase))
            ?? Specs.First(s => s.Family == "t3" && s.Size == "medium");
    }
}
