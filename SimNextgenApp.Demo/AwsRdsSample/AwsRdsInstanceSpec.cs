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
/// SlowSecs is modeled as FastSecs / BaselineFraction, assuming CPU-bound linear scaling.
/// This is a first-order approximation. The real-world performance depends on workload characteristics.
/// </summary>
/// <param name="BaselineFraction">The baseline CPU as a fraction (e.g., 0.20 for 20% baseline, 0.10 for 10%). Must be in range (0.0, 1.0].</param>
internal record BurstableInstanceSpec(
    string Family,
    string Size,
    int VCpus,
    double FastSecs,
    double EarnRatePerHour,
    double MaxCredits,
    double BaselineFraction
) : AwsRdsInstanceSpec(Family, Size, VCpus, FastSecs, ValidateAndCalculateSlowSecs(FastSecs, BaselineFraction))
{
    /// <summary>
    /// Validates BaselineFraction and calculates SlowSecs safely.
    /// </summary>
    private static double ValidateAndCalculateSlowSecs(double fastSecs, double baselineFraction)
    {
        if (baselineFraction <= 0 || baselineFraction > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                "BaselineFraction",  // Use literal to match public record parameter name (not nameof)
                baselineFraction,
                "BaselineFraction must be > 0 and <= 1.0 (e.g., 0.20 for 20% baseline).");
        }

        return fastSecs / baselineFraction;
    }
}

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
        // T3 - Burstable Intel (SlowSecs calculated from BaselineFraction)
        // Format: Family, Size, VCpus, FastSecs, EarnRatePerHour, MaxCredits, BaselineFraction
        new BurstableInstanceSpec("t3", "micro",  2, 0.050, 6,  144, 0.10),  // 0.10 = 10% baseline → SlowSecs = 0.500
        new BurstableInstanceSpec("t3", "small",  2, 0.050, 12, 288, 0.20),  // 0.20 = 20% baseline → SlowSecs = 0.250
        new BurstableInstanceSpec("t3", "medium", 2, 0.050, 24, 576, 0.20),  // 0.20 = 20% baseline → SlowSecs = 0.250
        new BurstableInstanceSpec("t3", "large",  2, 0.050, 36, 864, 0.30),  // 0.30 = 30% baseline → SlowSecs = 0.167

        // T4g - Burstable Graviton (Faster, similar baseline percentages)
        new BurstableInstanceSpec("t4g", "micro",  2, 0.040, 6,  144, 0.10),  // 0.10 = 10% baseline → SlowSecs = 0.400
        new BurstableInstanceSpec("t4g", "small",  2, 0.040, 12, 288, 0.20),  // 0.20 = 20% baseline → SlowSecs = 0.200
        new BurstableInstanceSpec("t4g", "medium", 2, 0.040, 24, 576, 0.20),  // 0.20 = 20% baseline → SlowSecs = 0.200
        new BurstableInstanceSpec("t4g", "large",  2, 0.040, 36, 864, 0.30),  // 0.30 = 30% baseline → SlowSecs = 0.133

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
