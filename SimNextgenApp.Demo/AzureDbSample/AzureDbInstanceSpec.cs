namespace SimNextgenApp.Demo.AzureDbSample;

/// <summary>
/// Base record for Azure Database instance specifications.
/// </summary>
internal abstract record AzureDbInstanceSpec(
    string Series,
    string Size,
    int VCores,
    double FastSecs,
    double SlowSecs
);

/// <summary>
/// Specification for burstable (B-series) instances.
/// SlowSecs is modeled as FastSecs / BaselineFraction, assuming CPU-bound linear scaling.
/// This is a first-order approximation. The real-world performance depends on workload characteristics.
/// </summary>
/// <param name="BaselineFraction">The baseline CPU as a fraction (e.g., 0.20 for 20% baseline). Must be in range (0.0, 1.0].</param>
internal record BurstableInstanceSpec(
    string Series,
    string Size,
    int VCores,
    double FastSecs,
    double EarnRatePerHour,
    double MaxCredits,
    double BaselineFraction
) : AzureDbInstanceSpec(Series, Size, VCores, FastSecs, ValidateAndCalculateSlowSecs(FastSecs, BaselineFraction))
{
    /// <summary>
    /// Validates BaselineFraction and calculates SlowSecs safely.
    /// </summary>
    private static double ValidateAndCalculateSlowSecs(double fastSecs, double baselineFraction)
    {
        if (baselineFraction <= 0 || baselineFraction > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                "BaselineFraction",
                baselineFraction,
                "BaselineFraction must be > 0 and <= 1.0 (e.g., 0.20 for 20% baseline).");
        }

        return fastSecs / baselineFraction;
    }
}

/// <summary>
/// Specification for fixed-performance (D, E series) instances.
/// </summary>
internal record FixedInstanceSpec(
    string Series,
    string Size,
    int VCores,
    double FastSecs
) : AzureDbInstanceSpec(Series, Size, VCores, FastSecs, FastSecs);

/// <summary>
/// Registry of supported Azure Database instance specifications.
/// </summary>
internal static class AzureDbRegistry
{
    private static readonly List<AzureDbInstanceSpec> Specs = new()
    {
        // B-series - Burstable (20% baseline for all)
        // Azure starts with ~30 credits per core (initial bank for boot-up)
        // Format: Series, Size, VCores, FastSecs, EarnRatePerHour, MaxCredits, BaselineFraction

        // IMPORTANT: FastSecs is identical across all B-series sizes (0.080s) because a single-threaded
        // query runs at the same speed regardless of vCore count - each vCore has identical performance.
        // Size-based performance differentiation comes from CONCURRENCY CAPACITY (VCores), not query speed.
        //
        // Example throughput at 50 req/sec arrival rate:
        //   B.1ms: 1 vCore  × 12.5 qps/core = 12.5 qps capacity -> 400% utilization (oversaturated)
        //   B.2ms: 2 vCores × 12.5 qps/core = 25 qps capacity   -> 200% utilization (saturated)
        //   B.4ms: 4 vCores × 12.5 qps/core = 50 qps capacity   -> 100% utilization (critical point)
        //   B.8ms: 8 vCores × 12.5 qps/core = 100 qps capacity  -> 50% utilization (comfortable)
        //
        // This models reality: larger instances win through PARALLELISM, not faster individual queries.
        // See: AzurePgsqlPoolingScenario.cs (numberOfServers: VCores)

        new BurstableInstanceSpec("B", "1ms", 1, 0.080, 12, 288,  0.20),
        new BurstableInstanceSpec("B", "2s",  2, 0.080, 24, 576,  0.20),
        new BurstableInstanceSpec("B", "2ms", 2, 0.080, 24, 576,  0.20),
        new BurstableInstanceSpec("B", "4ms", 4, 0.080, 48, 1152, 0.20),
        new BurstableInstanceSpec("B", "8ms", 8, 0.080, 96, 2304, 0.20),

        // Future: D-series (General Purpose), E-series (Memory Optimized)
    };

    public static AzureDbInstanceSpec GetSpec(string series, string size)
    {
        var spec = Specs.FirstOrDefault(s =>
            s.Series.Equals(series, StringComparison.OrdinalIgnoreCase) &&
            s.Size.Equals(size, StringComparison.OrdinalIgnoreCase));

        if (spec == null)
            throw new ArgumentException($"Unknown Azure Database instance: {series}.{size}");

        return spec;
    }
}
