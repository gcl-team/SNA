using Microsoft.Extensions.Logging;

namespace SimNextgenApp.Core.Utilities;

/// <summary>
/// Provides validation utilities to help users choose appropriate TimeUnit settings
/// and detect potential precision issues before running simulations.
/// </summary>
public static class SimulationProfileValidator
{
    /// <summary>
    /// Validates that the specified TimeUnit provides sufficient precision for the given time distribution.
    /// </summary>
    /// <param name="timeUnit">The simulation time unit to validate.</param>
    /// <param name="sampleFunction">A function that generates TimeSpan samples from the distribution being tested.</param>
    /// <param name="sampleSize">Number of samples to generate for testing. Default is 1000.</param>
    /// <param name="truncationThreshold">The maximum acceptable proportion of samples that truncate to 0. Default is 0.05 (5%).</param>
    /// <returns>A ValidationResult indicating whether the TimeUnit is appropriate and providing recommendations if not.</returns>
    /// <remarks>
    /// <para>
    /// This method samples the provided distribution and checks how many values would truncate to zero
    /// when converted to the specified simulation time units. If too many samples truncate, it suggests
    /// using a finer time unit.
    /// </para>
    /// </remarks>
    public static ValidationResult ValidateTimeUnit(
        SimulationTimeUnit timeUnit,
        Func<Random, TimeSpan> sampleFunction,
        int sampleSize = 1000,
        double truncationThreshold = 0.05)
    {
        ArgumentNullException.ThrowIfNull(sampleFunction);

        if (sampleSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be positive.");

        if (truncationThreshold < 0 || truncationThreshold > 1)
            throw new ArgumentOutOfRangeException(nameof(truncationThreshold), "Truncation threshold must be between 0 and 1.");

        var rnd = new Random(42); // Fixed seed for reproducibility
        int truncatedCount = 0;
        long minConverted = long.MaxValue;
        long maxConverted = long.MinValue;
        double minOriginalSeconds = double.MaxValue;
        double maxOriginalSeconds = double.MinValue;

        for (int i = 0; i < sampleSize; i++)
        {
            var sample = sampleFunction(rnd);
            double sampleSeconds = sample.TotalSeconds;

            minOriginalSeconds = Math.Min(minOriginalSeconds, sampleSeconds);
            maxOriginalSeconds = Math.Max(maxOriginalSeconds, sampleSeconds);

            long converted = TimeUnitConverter.ConvertToSimulationUnits(sample, timeUnit);

            minConverted = Math.Min(minConverted, converted);
            maxConverted = Math.Max(maxConverted, converted);

            if (converted == 0 && sample.Ticks > 0) // Non-zero duration truncated to 0
                truncatedCount++;
        }

        double truncationRate = (double)truncatedCount / sampleSize;

        if (truncationRate > truncationThreshold)
        {
            var recommendedUnit = SuggestFinerUnit(timeUnit);
            var unitName = TimeUnitConverter.GetUnitDisplayName(timeUnit);
            var recommendedName = TimeUnitConverter.GetUnitDisplayName(recommendedUnit);

            return new ValidationResult
            {
                IsValid = false,
                TimeUnit = timeUnit,
                TruncationRate = truncationRate,
                SampleSize = sampleSize,
                TruncatedCount = truncatedCount,
                MinValueInUnits = minConverted,
                MaxValueInUnits = maxConverted,
                MinOriginalSeconds = minOriginalSeconds,
                MaxOriginalSeconds = maxOriginalSeconds,
                RecommendedUnit = recommendedUnit,
                Message = $"WARNING: TimeUnit precision issue detected!\n" +
                         $"   Current TimeUnit: {unitName}\n" +
                         $"   Truncation rate: {truncationRate:P1} ({truncatedCount}/{sampleSize} samples truncate to 0)\n" +
                         $"   Sample range: {minOriginalSeconds:F6}s to {maxOriginalSeconds:F3}s\n" +
                         $"   Converted range: {minConverted} to {maxConverted} {unitName}\n" +
                         $"   RECOMMENDATION: Use SimulationTimeUnit.{recommendedUnit} instead.\n" +
                         $"   This will preserve sub-{unitName} precision and prevent events from collapsing to time 0."
            };
        }

        return new ValidationResult
        {
            IsValid = true,
            TimeUnit = timeUnit,
            TruncationRate = truncationRate,
            SampleSize = sampleSize,
            TruncatedCount = truncatedCount,
            MinValueInUnits = minConverted,
            MaxValueInUnits = maxConverted,
            MinOriginalSeconds = minOriginalSeconds,
            MaxOriginalSeconds = maxOriginalSeconds,
            RecommendedUnit = timeUnit,
            Message = $"TimeUnit validation passed: {TimeUnitConverter.GetUnitDisplayName(timeUnit)} provides sufficient precision.\n" +
                     $"   Sample range: {minOriginalSeconds:F6}s to {maxOriginalSeconds:F3}s\n" +
                     $"   Converted range: {minConverted} to {maxConverted} {TimeUnitConverter.GetUnitDisplayName(timeUnit)}\n" +
                     $"   Truncation rate: {truncationRate:P2} ({truncatedCount}/{sampleSize} samples)"
        };
    }

    /// <summary>
    /// Validates multiple time distributions (e.g., inter-arrival time and service time) against the specified TimeUnit.
    /// Returns the overall validation result, failing if any distribution fails validation.
    /// </summary>
    /// <param name="timeUnit">The simulation time unit to validate.</param>
    /// <param name="distributions">A dictionary of distribution names to sample functions.</param>
    /// <param name="sampleSize">Number of samples to generate per distribution. Default is 1000.</param>
    /// <param name="truncationThreshold">The maximum acceptable proportion of samples that truncate to 0. Default is 0.05 (5%).</param>
    /// <returns>A ValidationResult indicating the overall validation status.</returns>
    public static ValidationResult ValidateTimeUnit(
        SimulationTimeUnit timeUnit,
        Dictionary<string, Func<Random, TimeSpan>> distributions,
        int sampleSize = 1000,
        double truncationThreshold = 0.05)
    {
        ArgumentNullException.ThrowIfNull(distributions);

        if (distributions.Count == 0)
            throw new ArgumentException("Must provide at least one distribution to validate.", nameof(distributions));

        var results = new List<(string Name, ValidationResult Result)>();
        SimulationTimeUnit? recommendedUnit = null;
        double maxTruncationRate = 0;

        foreach (var kvp in distributions)
        {
            var result = ValidateTimeUnit(timeUnit, kvp.Value, sampleSize, truncationThreshold);
            results.Add((kvp.Key, result));

            if (!result.IsValid)
            {
                if (result.TruncationRate > maxTruncationRate)
                {
                    maxTruncationRate = result.TruncationRate;
                    recommendedUnit = result.RecommendedUnit;
                }
            }
        }

        bool allValid = results.All(r => r.Result.IsValid);

        if (!allValid)
        {
            var failedDistributions = results.Where(r => !r.Result.IsValid).ToList();
            var messageLines = new List<string>
            {
                $"WARNING: TimeUnit precision issue detected in {failedDistributions.Count} distribution(s)!",
                $"   Current TimeUnit: {TimeUnitConverter.GetUnitDisplayName(timeUnit)}",
                ""
            };

            foreach (var (name, result) in failedDistributions)
            {
                messageLines.Add($"   [{name}]");
                messageLines.Add($"      Truncation rate: {result.TruncationRate:P1} ({result.TruncatedCount}/{result.SampleSize} samples)");
                messageLines.Add($"      Sample range: {result.MinOriginalSeconds:F6}s to {result.MaxOriginalSeconds:F3}s");
                messageLines.Add("");
            }

            messageLines.Add($"   RECOMMENDATION: Use SimulationTimeUnit.{recommendedUnit} for all distributions.");

            return new ValidationResult
            {
                IsValid = false,
                TimeUnit = timeUnit,
                TruncationRate = maxTruncationRate,
                SampleSize = sampleSize * distributions.Count,
                RecommendedUnit = recommendedUnit!.Value,
                Message = string.Join('\n', messageLines)
            };
        }

        return new ValidationResult
        {
            IsValid = true,
            TimeUnit = timeUnit,
            TruncationRate = 0,
            SampleSize = sampleSize * distributions.Count,
            RecommendedUnit = timeUnit,
            Message = $"TimeUnit validation passed for all {distributions.Count} distribution(s): " +
                     $"{TimeUnitConverter.GetUnitDisplayName(timeUnit)} provides sufficient precision."
        };
    }

    /// <summary>
    /// Suggests a finer time unit than the current one to improve precision.
    /// </summary>
    private static SimulationTimeUnit SuggestFinerUnit(SimulationTimeUnit current)
    {
        return current switch
        {
            SimulationTimeUnit.Days => SimulationTimeUnit.Hours,
            SimulationTimeUnit.Hours => SimulationTimeUnit.Minutes,
            SimulationTimeUnit.Minutes => SimulationTimeUnit.Seconds,
            SimulationTimeUnit.Seconds => SimulationTimeUnit.Milliseconds,
            SimulationTimeUnit.Milliseconds => SimulationTimeUnit.Microseconds,
            SimulationTimeUnit.Microseconds => SimulationTimeUnit.Ticks,
            SimulationTimeUnit.Ticks => SimulationTimeUnit.Ticks, // Already finest
            _ => throw new ArgumentOutOfRangeException(nameof(current), $"Unsupported time unit: {current}")
        };
    }

    /// <summary>
    /// Logs validation results using the provided logger.
    /// </summary>
    /// <param name="result">The validation result to log.</param>
    /// <param name="logger">The logger to use. If null, no logging occurs.</param>
    public static void LogValidationResult(ValidationResult result, ILogger? logger)
    {
        if (logger == null) return;

        if (result.IsValid)
        {
            logger.LogInformation(result.Message);
        }
        else
        {
            logger.LogWarning(result.Message);
        }
    }
}

/// <summary>
/// Represents the result of validating a TimeUnit against a time distribution.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Indicates whether the TimeUnit provides sufficient precision for the distribution.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// The TimeUnit that was validated.
    /// </summary>
    public required SimulationTimeUnit TimeUnit { get; init; }

    /// <summary>
    /// The proportion of samples that truncated to zero (0.0 to 1.0).
    /// </summary>
    public required double TruncationRate { get; init; }

    /// <summary>
    /// The number of samples that were tested.
    /// </summary>
    public required int SampleSize { get; init; }

    /// <summary>
    /// The number of samples that truncated to zero.
    /// </summary>
    public int TruncatedCount { get; init; }

    /// <summary>
    /// The minimum value observed after conversion to simulation units.
    /// </summary>
    public long MinValueInUnits { get; init; }

    /// <summary>
    /// The maximum value observed after conversion to simulation units.
    /// </summary>
    public long MaxValueInUnits { get; init; }

    /// <summary>
    /// The minimum original sample value in seconds.
    /// </summary>
    public double MinOriginalSeconds { get; init; }

    /// <summary>
    /// The maximum original sample value in seconds.
    /// </summary>
    public double MaxOriginalSeconds { get; init; }

    /// <summary>
    /// The recommended TimeUnit to use if the current one is insufficient.
    /// </summary>
    public required SimulationTimeUnit RecommendedUnit { get; init; }

    /// <summary>
    /// A human-readable message describing the validation result and any recommendations.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// A pre-configured successful validation result for cases where validation is skipped.
    /// </summary>
    public static ValidationResult Success => new()
    {
        IsValid = true,
        TimeUnit = SimulationTimeUnit.Ticks,
        TruncationRate = 0.0,
        SampleSize = 0,
        RecommendedUnit = SimulationTimeUnit.Ticks,
        Message = "Validation skipped or passed."
    };
}
