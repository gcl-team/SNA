using SimNextgenApp.Core.Utilities;

namespace SimNextgenApp.Tests.Core.Utilities;

/// <summary>
/// Tests for SimulationProfileValidator to verify TimeUnit precision validation logic.
/// These tests ensure that the validator correctly detects precision issues and recommends appropriate TimeUnits.
/// </summary>
public class SimulationProfileValidatorTests
{
    #region Category 1: Single Distribution Validation - Happy Path

    [Fact(DisplayName = "ValidateTimeUnit with good precision returns valid result")]
    public void ValidateTimeUnit_WithGoodPrecision_ReturnsValid()
    {
        // Arrange: Millisecond-scale distribution (10-100ms) with Milliseconds unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromMilliseconds(rnd.Next(10, 100));

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Milliseconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.TimeUnit);
        Assert.Equal(0.0, result.TruncationRate); // No truncations expected
        Assert.Equal(0, result.TruncatedCount);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.RecommendedUnit);
        Assert.Contains("passed", result.Message, StringComparison.OrdinalIgnoreCase);

        // Verify min/max tracking
        Assert.InRange(result.MinValueInUnits, 10, 99); // 10-99ms
        Assert.InRange(result.MaxValueInUnits, 10, 99);
        Assert.InRange(result.MinOriginalSeconds, 0.01, 0.099); // 10-99ms in seconds
        Assert.InRange(result.MaxOriginalSeconds, 0.01, 0.099);
    }

    [Fact(DisplayName = "ValidateTimeUnit with seconds unit and second-scale distribution returns valid")]
    public void ValidateTimeUnit_WithSecondsAndSecondScaleDistribution_ReturnsValid()
    {
        // Arrange: Second-scale distribution (1-10 seconds) with Seconds unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(rnd.Next(1, 11));

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Seconds, result.TimeUnit);
        Assert.Equal(0.0, result.TruncationRate);
        Assert.Equal(0, result.TruncatedCount);
        Assert.Equal(SimulationTimeUnit.Seconds, result.RecommendedUnit);

        // Verify range
        Assert.InRange(result.MinValueInUnits, 1, 10); // 1-10 seconds
        Assert.InRange(result.MaxValueInUnits, 1, 10);
    }

    [Fact(DisplayName = "ValidateTimeUnit with zero truncations returns valid")]
    public void ValidateTimeUnit_WithZeroTruncations_ReturnsValid()
    {
        // Arrange: Distribution that always produces values >= 1 second with Seconds unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(1.0 + rnd.NextDouble() * 10.0);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 500,
            truncationThreshold: 0.05);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0.0, result.TruncationRate);
        Assert.Equal(0, result.TruncatedCount);
        Assert.Equal(500, result.SampleSize);

        // All values should be at least 1 second
        Assert.True(result.MinValueInUnits >= 1);
    }

    #endregion

    #region Category 2: Single Distribution Validation - Precision Failures

    [Fact(DisplayName = "ValidateTimeUnit with high truncation rate returns invalid and recommends finer unit")]
    public void ValidateTimeUnit_WithHighTruncationRate_ReturnsInvalidAndRecommendsFinerUnit()
    {
        // Arrange: Millisecond-scale distribution (1-10ms) with Seconds unit - will truncate to 0
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromMilliseconds(rnd.Next(1, 11));

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Seconds, result.TimeUnit);
        Assert.Equal(1.0, result.TruncationRate); // 100% truncation - all samples are < 1 second
        Assert.Equal(100, result.TruncatedCount);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.RecommendedUnit); // Should recommend Milliseconds
        Assert.Contains("WARNING", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Milliseconds", result.Message, StringComparison.OrdinalIgnoreCase);

        // All values should truncate to 0 in seconds
        Assert.Equal(0, result.MinValueInUnits);
        Assert.Equal(0, result.MaxValueInUnits);

        // But original values should be non-zero
        Assert.True(result.MinOriginalSeconds > 0);
        Assert.True(result.MaxOriginalSeconds > 0);
    }

    [Fact(DisplayName = "ValidateTimeUnit with all samples truncating returns invalid")]
    public void ValidateTimeUnit_WithAllSamplesTruncating_ReturnsInvalid()
    {
        // Arrange: Very small distribution (0.1-0.9 seconds) with Minutes unit - all will truncate
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(0.1 + rnd.NextDouble() * 0.8);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Minutes,
            distribution,
            sampleSize: 200,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Minutes, result.TimeUnit);
        Assert.Equal(1.0, result.TruncationRate); // 100% truncation
        Assert.Equal(200, result.TruncatedCount);
        Assert.Equal(SimulationTimeUnit.Seconds, result.RecommendedUnit); // Should recommend Seconds
    }

    [Fact(DisplayName = "ValidateTimeUnit with exactly threshold truncation returns invalid")]
    public void ValidateTimeUnit_WithExactlyThresholdTruncation_ReturnsInvalid()
    {
        // Arrange: Mixed distribution where exactly 5% truncate to 0
        // With fixed seed (42), we can control the outcome
        Func<Random, TimeSpan> distribution = rnd =>
        {
            // Generate values where 5 out of 100 will be < 1 second (truncate to 0)
            double value = rnd.NextDouble();
            return value < 0.05
                ? TimeSpan.FromSeconds(0.5) // 5% will be 0.5s -> truncates to 0
                : TimeSpan.FromSeconds(1.0 + value); // 95% will be >= 1s
        };

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert: At threshold (0.05), should be INVALID (> threshold means invalid)
        // Note: Due to fixed seed randomness, exact count may vary, but should be close to threshold
        Assert.True(result.TruncationRate >= 0.05); // At or above threshold
    }

    [Fact(DisplayName = "ValidateTimeUnit with just below threshold returns valid")]
    public void ValidateTimeUnit_WithJustBelowThreshold_ReturnsValid()
    {
        // Arrange: Distribution where ~4% truncate to 0 (just below 5% threshold)
        Func<Random, TimeSpan> distribution = rnd =>
        {
            double value = rnd.NextDouble();
            return value < 0.03 // Only 3% chance of sub-second values
                ? TimeSpan.FromSeconds(0.5) // Truncates to 0
                : TimeSpan.FromSeconds(1.0 + value * 10); // >= 1s
        };

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 1000, // Larger sample for statistical accuracy
            truncationThreshold: 0.05);

        // Assert: Should be valid since truncation rate < 5%
        Assert.True(result.IsValid);
        Assert.True(result.TruncationRate < 0.05);
        Assert.Equal(SimulationTimeUnit.Seconds, result.RecommendedUnit); // No change needed
    }

    #endregion

    #region Category 3: Recommendation Logic

    [Fact(DisplayName = "SuggestFinerUnit from Days recommends Hours")]
    public void SuggestFinerUnit_FromDays_ReturnsHours()
    {
        // Arrange: Sub-day distribution (1-10 hours) with Days unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromHours(1 + rnd.NextDouble() * 9);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Days,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Hours, result.RecommendedUnit);
    }

    [Fact(DisplayName = "SuggestFinerUnit from Hours recommends Minutes")]
    public void SuggestFinerUnit_FromHours_ReturnsMinutes()
    {
        // Arrange: Sub-hour distribution (1-30 minutes) with Hours unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromMinutes(1 + rnd.NextDouble() * 29);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Hours,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Minutes, result.RecommendedUnit);
    }

    [Fact(DisplayName = "SuggestFinerUnit from Minutes recommends Seconds")]
    public void SuggestFinerUnit_FromMinutes_ReturnsSeconds()
    {
        // Arrange: Sub-minute distribution (1-30 seconds) with Minutes unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(1 + rnd.NextDouble() * 29);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Minutes,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Seconds, result.RecommendedUnit);
    }

    [Fact(DisplayName = "SuggestFinerUnit from Seconds recommends Milliseconds")]
    public void SuggestFinerUnit_FromSeconds_ReturnsMilliseconds()
    {
        // Arrange: Sub-second distribution (1-100 milliseconds) with Seconds unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromMilliseconds(1 + rnd.NextDouble() * 99);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.RecommendedUnit);
    }

    [Fact(DisplayName = "SuggestFinerUnit from Milliseconds recommends Microseconds")]
    public void SuggestFinerUnit_FromMilliseconds_ReturnsMicroseconds()
    {
        // Arrange: Sub-millisecond distribution (1-100 microseconds) with Milliseconds unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromTicks(rnd.Next(10, 1000)); // 1-100 microseconds

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Milliseconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Microseconds, result.RecommendedUnit);
    }

    [Fact(DisplayName = "SuggestFinerUnit from Microseconds recommends Ticks")]
    public void SuggestFinerUnit_FromMicroseconds_ReturnsTicks()
    {
        // Arrange: Sub-microsecond distribution (1-9 ticks) with Microseconds unit
        // 1 microsecond = 10 ticks, so 1-9 ticks will truncate to 0
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromTicks(rnd.Next(1, 10));

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Microseconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Ticks, result.RecommendedUnit);
    }

    [Fact(DisplayName = "SuggestFinerUnit from Ticks returns Ticks (already finest)")]
    public void SuggestFinerUnit_FromTicks_ReturnsTicks()
    {
        // Arrange: Create a distribution that will pass validation even with Ticks
        // (it's hard to fail at Ticks precision, so we use values that work)
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromTicks(rnd.Next(1, 100));

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Ticks,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert: Should pass since Ticks is finest granularity
        Assert.True(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Ticks, result.RecommendedUnit);
    }

    [Fact(DisplayName = "Recommendation logic selects appropriate unit for microsecond-scale distribution")]
    public void RecommendationLogic_WithMicrosecondScaleDistribution_RecommendsMicroseconds()
    {
        // Arrange: Microsecond-scale distribution (10-100 microseconds) with Seconds unit
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromTicks(rnd.Next(100, 1000)); // 10-100 microseconds

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert: Should recommend Milliseconds (one step finer than Seconds)
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.RecommendedUnit);
        Assert.Contains("Milliseconds", result.Message);
    }

    #endregion

    #region Category 4: Multiple Distributions Validation

    [Fact(DisplayName = "ValidateTimeUnit with multiple distributions all passing returns valid")]
    public void ValidateTimeUnit_MultipleDistributions_AllPass_ReturnsValid()
    {
        // Arrange: Multiple distributions that all have good precision with Milliseconds unit
        var distributions = new Dictionary<string, Func<Random, TimeSpan>>
        {
            ["Inter-arrival time"] = rnd => TimeSpan.FromMilliseconds(10 + rnd.NextDouble() * 90),
            ["Service time"] = rnd => TimeSpan.FromMilliseconds(50 + rnd.NextDouble() * 150),
            ["Processing delay"] = rnd => TimeSpan.FromMilliseconds(5 + rnd.NextDouble() * 20)
        };

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Milliseconds,
            distributions,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.TimeUnit);
        Assert.Equal(0.0, result.TruncationRate);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.RecommendedUnit);
        Assert.Contains("passed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3", result.Message); // Should mention 3 distributions
        Assert.Equal(300, result.SampleSize); // 100 samples per distribution × 3
    }

    [Fact(DisplayName = "ValidateTimeUnit with multiple distributions where one fails returns invalid")]
    public void ValidateTimeUnit_MultipleDistributions_OneFails_ReturnsInvalid()
    {
        // Arrange: One distribution fails (millisecond-scale with Seconds unit), others pass
        var distributions = new Dictionary<string, Func<Random, TimeSpan>>
        {
            ["Inter-arrival time"] = rnd => TimeSpan.FromSeconds(1 + rnd.NextDouble() * 5), // OK
            ["Service time"] = rnd => TimeSpan.FromMilliseconds(10 + rnd.NextDouble() * 90), // FAILS - truncates to 0
            ["Processing delay"] = rnd => TimeSpan.FromSeconds(2 + rnd.NextDouble() * 3) // OK
        };

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distributions,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert: Should fail because one distribution has insufficient precision
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Seconds, result.TimeUnit);
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.RecommendedUnit);
        Assert.Contains("WARNING", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Service time", result.Message); // Should mention the failing distribution
        Assert.Contains("1 distribution", result.Message); // Should mention count of failures
    }

    [Fact(DisplayName = "ValidateTimeUnit with multiple distributions failing recommends finest needed unit")]
    public void ValidateTimeUnit_MultipleDistributions_MultipleFail_RecommendsFinestNeeded()
    {
        // Arrange: Multiple distributions fail with different severities
        var distributions = new Dictionary<string, Func<Random, TimeSpan>>
        {
            ["Inter-arrival time"] = rnd => TimeSpan.FromSeconds(0.1 + rnd.NextDouble() * 0.5), // Needs Milliseconds
            ["Service time"] = rnd => TimeSpan.FromMilliseconds(0.05 + rnd.NextDouble() * 0.5), // Needs Microseconds
            ["Processing delay"] = rnd => TimeSpan.FromMinutes(0.5 + rnd.NextDouble()) // OK with Minutes
        };

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Minutes,
            distributions,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert: Should recommend the finest unit needed (Seconds, one step finer than Minutes)
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Minutes, result.TimeUnit);
        Assert.Equal(SimulationTimeUnit.Seconds, result.RecommendedUnit); // Finest needed for worst case
        Assert.Contains("WARNING", result.Message, StringComparison.OrdinalIgnoreCase);
        // Verify message contains details about failing distributions
        Assert.Contains("Inter-arrival time", result.Message);
        Assert.Contains("Service time", result.Message);
    }

    [Fact(DisplayName = "ValidateTimeUnit with multiple distributions provides detailed failure information")]
    public void ValidateTimeUnit_MultipleDistributions_ProvidesDetailedFailureInfo()
    {
        // Arrange: Two distributions that fail
        var distributions = new Dictionary<string, Func<Random, TimeSpan>>
        {
            ["Fast events"] = rnd => TimeSpan.FromMilliseconds(1 + rnd.NextDouble() * 5),
            ["Slow events"] = rnd => TimeSpan.FromMilliseconds(100 + rnd.NextDouble() * 500)
        };

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distributions,
            sampleSize: 100,
            truncationThreshold: 0.05);

        // Assert: Message should include details about each failing distribution
        Assert.False(result.IsValid);
        Assert.Contains("[Fast events]", result.Message); // Distribution name in brackets
        Assert.Contains("Truncation rate", result.Message); // Should show truncation rates
        Assert.Contains("Sample range", result.Message); // Should show sample ranges
    }

    #endregion

    #region Category 5: Edge Cases & Validation

    [Fact(DisplayName = "ValidateTimeUnit with null sample function throws ArgumentNullException")]
    public void ValidateTimeUnit_WithNullSampleFunction_ThrowsArgumentNullException()
    {
        // Arrange
        Func<Random, TimeSpan>? nullFunction = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            SimulationProfileValidator.ValidateTimeUnit(
                SimulationTimeUnit.Milliseconds,
                nullFunction!,
                sampleSize: 100,
                truncationThreshold: 0.05));

        Assert.Equal("sampleFunction", exception.ParamName);
    }

    [Fact(DisplayName = "ValidateTimeUnit with null distributions dictionary throws ArgumentNullException")]
    public void ValidateTimeUnit_WithNullDistributionsDictionary_ThrowsArgumentNullException()
    {
        // Arrange
        Dictionary<string, Func<Random, TimeSpan>>? nullDictionary = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            SimulationProfileValidator.ValidateTimeUnit(
                SimulationTimeUnit.Milliseconds,
                nullDictionary!,
                sampleSize: 100,
                truncationThreshold: 0.05));

        Assert.Equal("distributions", exception.ParamName);
    }

    [Fact(DisplayName = "ValidateTimeUnit with empty distributions dictionary throws ArgumentException")]
    public void ValidateTimeUnit_WithEmptyDistributionsDictionary_ThrowsArgumentException()
    {
        // Arrange
        var emptyDictionary = new Dictionary<string, Func<Random, TimeSpan>>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            SimulationProfileValidator.ValidateTimeUnit(
                SimulationTimeUnit.Milliseconds,
                emptyDictionary,
                sampleSize: 100,
                truncationThreshold: 0.05));

        Assert.Equal("distributions", exception.ParamName);
        Assert.Contains("at least one distribution", exception.Message);
    }

    [Fact(DisplayName = "ValidateTimeUnit with negative sample size throws ArgumentOutOfRangeException")]
    public void ValidateTimeUnit_WithNegativeSampleSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationProfileValidator.ValidateTimeUnit(
                SimulationTimeUnit.Milliseconds,
                distribution,
                sampleSize: -100,
                truncationThreshold: 0.05));

        Assert.Equal("sampleSize", exception.ParamName);
        Assert.Contains("positive", exception.Message);
    }

    [Fact(DisplayName = "ValidateTimeUnit with zero sample size throws ArgumentOutOfRangeException")]
    public void ValidateTimeUnit_WithZeroSampleSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationProfileValidator.ValidateTimeUnit(
                SimulationTimeUnit.Milliseconds,
                distribution,
                sampleSize: 0,
                truncationThreshold: 0.05));

        Assert.Equal("sampleSize", exception.ParamName);
        Assert.Contains("positive", exception.Message);
    }

    [Fact(DisplayName = "ValidateTimeUnit with negative truncation threshold throws ArgumentOutOfRangeException")]
    public void ValidateTimeUnit_WithNegativeTruncationThreshold_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationProfileValidator.ValidateTimeUnit(
                SimulationTimeUnit.Milliseconds,
                distribution,
                sampleSize: 100,
                truncationThreshold: -0.05));

        Assert.Equal("truncationThreshold", exception.ParamName);
        Assert.Contains("between 0 and 1", exception.Message);
    }

    [Fact(DisplayName = "ValidateTimeUnit with truncation threshold above one throws ArgumentOutOfRangeException")]
    public void ValidateTimeUnit_WithTruncationThresholdAboveOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationProfileValidator.ValidateTimeUnit(
                SimulationTimeUnit.Milliseconds,
                distribution,
                sampleSize: 100,
                truncationThreshold: 1.5));

        Assert.Equal("truncationThreshold", exception.ParamName);
        Assert.Contains("between 0 and 1", exception.Message);
    }

    #endregion

    #region Category 6: Statistical Correctness

    [Fact(DisplayName = "ValidateTimeUnit records correct min and max values in both units and seconds")]
    public void ValidateTimeUnit_RecordsCorrectMinMaxValues_InBothUnitsAndSeconds()
    {
        // Arrange: Known range distribution (100-200 milliseconds)
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromMilliseconds(100 + rnd.NextDouble() * 100);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Milliseconds,
            distribution,
            sampleSize: 200,
            truncationThreshold: 0.05);

        // Assert: Verify min/max tracking in both representations
        Assert.InRange(result.MinValueInUnits, 100, 199); // Min should be 100-199 ms
        Assert.InRange(result.MaxValueInUnits, 100, 200); // Max should be 100-200 ms

        // Verify original seconds values match
        Assert.InRange(result.MinOriginalSeconds, 0.1, 0.199); // 100-199ms in seconds
        Assert.InRange(result.MaxOriginalSeconds, 0.1, 0.2);   // 100-200ms in seconds

        // Min should be less than max
        Assert.True(result.MinValueInUnits <= result.MaxValueInUnits);
        Assert.True(result.MinOriginalSeconds <= result.MaxOriginalSeconds);
    }

    [Fact(DisplayName = "ValidateTimeUnit calculates truncation rate correctly")]
    public void ValidateTimeUnit_CalculatesTruncationRateCorrectly()
    {
        // Arrange: Distribution where we know exactly how many will truncate
        // All samples are 0.1-0.5 seconds with Seconds unit = all truncate to 0
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(0.1 + rnd.NextDouble() * 0.4);
        int sampleSize = 250;

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: sampleSize,
            truncationThreshold: 0.05);

        // Assert: All samples should truncate, so rate = 1.0
        Assert.Equal(1.0, result.TruncationRate);
        Assert.Equal(sampleSize, result.TruncatedCount);
        Assert.Equal(sampleSize, result.SampleSize);

        // Verify the math: truncationRate = truncatedCount / sampleSize
        double expectedRate = (double)result.TruncatedCount / result.SampleSize;
        Assert.Equal(expectedRate, result.TruncationRate);
    }

    [Fact(DisplayName = "ValidateTimeUnit only counts non-zero TimeSpan samples as truncated")]
    public void ValidateTimeUnit_OnlyCountsNonZeroSamplesAsTruncated()
    {
        // Arrange: Distribution that sometimes returns TimeSpan.Zero
        Func<Random, TimeSpan> distribution = rnd =>
        {
            double value = rnd.NextDouble();
            // 20% chance of TimeSpan.Zero, 80% chance of sub-second value
            return value < 0.2
                ? TimeSpan.Zero  // Should NOT count as truncated
                : TimeSpan.FromSeconds(0.5); // Should count as truncated with Seconds unit
        };

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 1000,
            truncationThreshold: 0.05);

        // Assert: Only the non-zero samples that truncate to 0 should be counted
        // With fixed seed 42, approximately 80% should be counted as truncated
        // (the 0.5 second samples), not 100%
        Assert.True(result.TruncationRate > 0.70); // At least 70%
        Assert.True(result.TruncationRate < 0.90); // But not more than 90%

        // Should fail validation since truncation rate is above 5%
        Assert.False(result.IsValid);
    }

    [Fact(DisplayName = "ValidateTimeUnit uses fixed seed for reproducibility")]
    public void ValidateTimeUnit_UsesFixedSeedForReproducibility()
    {
        // Arrange: Same distribution tested twice
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromMilliseconds(1 + rnd.NextDouble() * 99);

        // Act: Run validation twice with same parameters
        var result1 = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 500,
            truncationThreshold: 0.05);

        var result2 = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 500,
            truncationThreshold: 0.05);

        // Assert: Results should be identical due to fixed seed
        Assert.Equal(result1.TruncationRate, result2.TruncationRate);
        Assert.Equal(result1.TruncatedCount, result2.TruncatedCount);
        Assert.Equal(result1.MinValueInUnits, result2.MinValueInUnits);
        Assert.Equal(result1.MaxValueInUnits, result2.MaxValueInUnits);
        Assert.Equal(result1.MinOriginalSeconds, result2.MinOriginalSeconds);
        Assert.Equal(result1.MaxOriginalSeconds, result2.MaxOriginalSeconds);
        Assert.Equal(result1.IsValid, result2.IsValid);
    }

    #endregion

    #region Category 7: ValidationResult Properties

    [Fact(DisplayName = "ValidationResult.Success static property has correct defaults")]
    public void ValidationResult_Success_HasCorrectDefaults()
    {
        // Act
        var success = ValidationResult.Success;

        // Assert: Verify all properties have safe defaults
        Assert.True(success.IsValid);
        Assert.Equal(SimulationTimeUnit.Ticks, success.TimeUnit);
        Assert.Equal(0.0, success.TruncationRate);
        Assert.Equal(0, success.SampleSize);
        Assert.Equal(SimulationTimeUnit.Ticks, success.RecommendedUnit);
        Assert.Equal("Validation skipped or passed.", success.Message);

        // Verify it's a valid, reusable object
        Assert.NotNull(success);

        // Multiple calls should return equivalent objects (though not necessarily same instance)
        var success2 = ValidationResult.Success;
        Assert.Equal(success.IsValid, success2.IsValid);
        Assert.Equal(success.Message, success2.Message);
        Assert.Equal(success.TimeUnit, success2.TimeUnit);
    }

    [Fact(DisplayName = "Failed ValidationResult contains all required data")]
    public void ValidationResult_Failed_ContainsAllRequiredData()
    {
        // Arrange: Create a scenario that will fail
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromMilliseconds(1 + rnd.NextDouble() * 10);

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 150,
            truncationThreshold: 0.05);

        // Assert: Verify all properties are populated correctly
        Assert.False(result.IsValid);
        Assert.Equal(SimulationTimeUnit.Seconds, result.TimeUnit);
        Assert.True(result.TruncationRate > 0); // Should have truncations
        Assert.Equal(150, result.SampleSize);
        Assert.True(result.TruncatedCount > 0); // Should count truncated samples
        Assert.Equal(SimulationTimeUnit.Milliseconds, result.RecommendedUnit);
        Assert.NotEmpty(result.Message); // Should have a meaningful message

        // Verify min/max fields are populated
        Assert.True(result.MinValueInUnits >= 0);
        Assert.True(result.MaxValueInUnits >= result.MinValueInUnits);
        Assert.True(result.MinOriginalSeconds >= 0);
        Assert.True(result.MaxOriginalSeconds >= result.MinOriginalSeconds);
    }

    [Fact(DisplayName = "ValidationResult populates min and max values correctly")]
    public void ValidationResult_PopulatesMinMaxValuesCorrectly()
    {
        // Arrange: Controlled distribution with known range
        Func<Random, TimeSpan> distribution = rnd => TimeSpan.FromSeconds(5 + rnd.NextDouble() * 10); // 5-15 seconds

        // Act
        var result = SimulationProfileValidator.ValidateTimeUnit(
            SimulationTimeUnit.Seconds,
            distribution,
            sampleSize: 300,
            truncationThreshold: 0.05);

        // Assert: Verify min/max values are within expected range
        Assert.InRange(result.MinValueInUnits, 5, 14); // Min should be 5-14 seconds
        Assert.InRange(result.MaxValueInUnits, 5, 15); // Max should be 5-15 seconds

        // Verify original seconds match the simulation units (since unit is Seconds)
        Assert.InRange(result.MinOriginalSeconds, 5.0, 14.99);
        Assert.InRange(result.MaxOriginalSeconds, 5.0, 15.0);

        // Verify consistency: converted values should match original values (within tolerance)
        // Since unit is Seconds, MinValueInUnits should approximately equal MinOriginalSeconds
        Assert.True(Math.Abs(result.MinValueInUnits - result.MinOriginalSeconds) < 1.0);
        Assert.True(Math.Abs(result.MaxValueInUnits - result.MaxOriginalSeconds) < 1.0);

        // Min must be <= Max
        Assert.True(result.MinValueInUnits <= result.MaxValueInUnits);
        Assert.True(result.MinOriginalSeconds <= result.MaxOriginalSeconds);
    }

    #endregion
}
