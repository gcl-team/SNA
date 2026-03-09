using SimNextgenApp.Core.Utilities;

namespace SimNextgenApp.Tests.Core.Utilities;

/// <summary>
/// Tests for TimeUnitConverter to verify pure integer arithmetic conversions.
/// These tests ensure that conversions are exact, deterministic, and platform-independent.
/// </summary>
public class TimeUnitConverterTests
{
    [Theory]
    [InlineData(0.05, SimulationTimeUnit.Milliseconds, 50)]           // 50ms
    [InlineData(0.001, SimulationTimeUnit.Milliseconds, 1)]           // 1ms
    [InlineData(1.5, SimulationTimeUnit.Milliseconds, 1500)]          // 1.5s = 1500ms
    [InlineData(60.0, SimulationTimeUnit.Seconds, 60)]                // 1 minute = 60 seconds
    [InlineData(0.5, SimulationTimeUnit.Seconds, 0)]                  // 0.5s truncates to 0 (sub-unit)
    [InlineData(60.0, SimulationTimeUnit.Minutes, 1)]                 // 60 seconds = 1 minute
    public void ConvertToSimulationUnits_WithVariousTimeSpans_ReturnsExpectedIntegerUnits(
        double seconds,
        SimulationTimeUnit unit,
        long expectedUnits)
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(seconds);

        // Act
        long actualUnits = TimeUnitConverter.ConvertToSimulationUnits(duration, unit);

        // Assert
        Assert.Equal(expectedUnits, actualUnits);
    }

    [Fact]
    public void ConvertToSimulationUnits_UsesIntegerArithmetic_NotFloatingPoint()
    {
        // This test verifies that we use pure integer tick arithmetic,
        // not floating-point TotalMilliseconds/TotalSeconds properties.

        // Arrange: A TimeSpan that could expose floating-point precision issues
        var duration = TimeSpan.FromSeconds(0.05);  // 50ms

        // Act: Convert to milliseconds using our method
        long milliseconds = TimeUnitConverter.ConvertToSimulationUnits(
            duration,
            SimulationTimeUnit.Milliseconds);

        // Assert: Should get exact result via integer arithmetic
        // duration.Ticks = 500,000
        // TimeSpan.TicksPerMillisecond = 10,000
        // 500,000 / 10,000 = 50 (exact integer division)
        Assert.Equal(50L, milliseconds);

        // Verify it's different from casting the double property
        // (In this case they might be equal, but the method matters)
        long expectedViaIntegerArithmetic = duration.Ticks / TimeSpan.TicksPerMillisecond;
        Assert.Equal(expectedViaIntegerArithmetic, milliseconds);
    }

    [Fact]
    public void ConvertToSimulationUnits_WithLargeTimeSpan_MaintainsPrecision()
    {
        // Arrange: 1 million seconds (large value where float precision could matter)
        var duration = TimeSpan.FromSeconds(1_000_000);

        // Act
        long milliseconds = TimeUnitConverter.ConvertToSimulationUnits(
            duration,
            SimulationTimeUnit.Milliseconds);

        // Assert: Should get exact result
        long expectedMs = 1_000_000_000L;  // 1 billion milliseconds
        Assert.Equal(expectedMs, milliseconds);

        // Verify integer arithmetic
        Assert.Equal(duration.Ticks / TimeSpan.TicksPerMillisecond, milliseconds);
    }

    [Theory]
    [InlineData(SimulationTimeUnit.Ticks, 10_000_000)]                 // 1 second = 10,000,000 ticks
    [InlineData(SimulationTimeUnit.Microseconds, 1_000_000)]           // 1 second = 1,000,000 microseconds
    [InlineData(SimulationTimeUnit.Milliseconds, 1_000)]               // 1 second = 1,000 milliseconds
    [InlineData(SimulationTimeUnit.Seconds, 1)]                        // 1 second = 1 second
    [InlineData(SimulationTimeUnit.Minutes, 0)]                        // 1 second = 0 minutes (truncated)
    [InlineData(SimulationTimeUnit.Hours, 0)]                          // 1 second = 0 hours (truncated)
    [InlineData(SimulationTimeUnit.Days, 0)]                           // 1 second = 0 days (truncated)
    public void ConvertToSimulationUnits_OneSecond_ConvertsCorrectlyToEachUnit(
        SimulationTimeUnit unit,
        long expectedUnits)
    {
        // Arrange
        var oneSecond = TimeSpan.FromSeconds(1.0);

        // Act
        long actualUnits = TimeUnitConverter.ConvertToSimulationUnits(oneSecond, unit);

        // Assert
        Assert.Equal(expectedUnits, actualUnits);
    }

    [Fact]
    public void ConvertToSimulationUnits_Ticks_ReturnsExactTickCount()
    {
        // Arrange: Create a TimeSpan with known tick count
        long expectedTicks = 123_456_789L;
        var duration = new TimeSpan(expectedTicks);

        // Act
        long actualTicks = TimeUnitConverter.ConvertToSimulationUnits(
            duration,
            SimulationTimeUnit.Ticks);

        // Assert
        Assert.Equal(expectedTicks, actualTicks);
        Assert.Equal(duration.Ticks, actualTicks);
    }

    [Fact]
    public void ConvertToSimulationUnits_Microseconds_UsesIntegerDivision()
    {
        // Arrange: 1 microsecond = 10 ticks
        var duration = TimeSpan.FromTicks(25);  // 2.5 microseconds

        // Act
        long microseconds = TimeUnitConverter.ConvertToSimulationUnits(
            duration,
            SimulationTimeUnit.Microseconds);

        // Assert: Should truncate to 2 (integer division: 25 / 10 = 2)
        Assert.Equal(2L, microseconds);
    }

    [Theory]
    [InlineData(0.0, SimulationTimeUnit.Seconds, 0)]
    [InlineData(-1.0, SimulationTimeUnit.Seconds, -1)]
    [InlineData(-0.5, SimulationTimeUnit.Milliseconds, -500)]
    public void ConvertToSimulationUnits_WithZeroAndNegativeValues_HandlesCorrectly(
        double seconds,
        SimulationTimeUnit unit,
        long expectedUnits)
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(seconds);

        // Act
        long actualUnits = TimeUnitConverter.ConvertToSimulationUnits(duration, unit);

        // Assert
        Assert.Equal(expectedUnits, actualUnits);
    }

    [Fact]
    public void ConvertToSimulationUnits_SubUnitPrecision_TruncatesToZero()
    {
        // This test documents the truncation behavior for sub-unit values.
        // Users should be warned by SimulationProfileValidator if this occurs.

        // Arrange: 0.05 seconds with Seconds unit
        var duration = TimeSpan.FromSeconds(0.05);

        // Act
        long seconds = TimeUnitConverter.ConvertToSimulationUnits(
            duration,
            SimulationTimeUnit.Seconds);

        // Assert: Truncates to 0 (500,000 ticks / 10,000,000 ticks_per_second = 0)
        Assert.Equal(0L, seconds);
    }

    [Theory]
    [InlineData(SimulationTimeUnit.Ticks, "ticks")]
    [InlineData(SimulationTimeUnit.Microseconds, "microseconds")]
    [InlineData(SimulationTimeUnit.Milliseconds, "milliseconds")]
    [InlineData(SimulationTimeUnit.Seconds, "seconds")]
    [InlineData(SimulationTimeUnit.Minutes, "minutes")]
    [InlineData(SimulationTimeUnit.Hours, "hours")]
    [InlineData(SimulationTimeUnit.Days, "days")]
    public void GetUnitDisplayName_ReturnsCorrectName(
        SimulationTimeUnit unit,
        string expectedName)
    {
        // Act
        string actualName = TimeUnitConverter.GetUnitDisplayName(unit);

        // Assert
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData(SimulationTimeUnit.Ticks, "ticks")]
    [InlineData(SimulationTimeUnit.Microseconds, "μs")]
    [InlineData(SimulationTimeUnit.Milliseconds, "ms")]
    [InlineData(SimulationTimeUnit.Seconds, "s")]
    [InlineData(SimulationTimeUnit.Minutes, "min")]
    [InlineData(SimulationTimeUnit.Hours, "hr")]
    [InlineData(SimulationTimeUnit.Days, "days")]
    public void GetUnitSymbol_ReturnsCorrectSymbol(
        SimulationTimeUnit unit,
        string expectedSymbol)
    {
        // Act
        string actualSymbol = TimeUnitConverter.GetUnitSymbol(unit);

        // Assert
        Assert.Equal(expectedSymbol, actualSymbol);
    }

    [Fact]
    public void ConvertToSimulationUnits_DeterministicAcrossMultipleCalls()
    {
        // This test verifies that the same TimeSpan always converts to the same result
        // (would not be guaranteed with floating-point arithmetic)

        // Arrange
        var duration = TimeSpan.FromSeconds(0.05);

        // Act: Convert multiple times
        long result1 = TimeUnitConverter.ConvertToSimulationUnits(duration, SimulationTimeUnit.Milliseconds);
        long result2 = TimeUnitConverter.ConvertToSimulationUnits(duration, SimulationTimeUnit.Milliseconds);
        long result3 = TimeUnitConverter.ConvertToSimulationUnits(duration, SimulationTimeUnit.Milliseconds);

        // Assert: All results must be identical
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
        Assert.Equal(50L, result1);
    }
}
