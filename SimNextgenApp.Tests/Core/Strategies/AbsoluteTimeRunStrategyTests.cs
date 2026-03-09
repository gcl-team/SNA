using SimNextgenApp.Core.Strategies;

namespace SimNextgenApp.Tests.Core.Strategies;

public class AbsoluteTimeRunStrategyTests
{
    [Fact(DisplayName = "Constructor should set WarmupEndTime when a valid value is provided.")]
    public void Constructor_WithValidWarmupTime_SetsWarmupProperty()
    {
        // Arrange & Act
        var strategy = new AbsoluteTimeRunStrategy(100, 10);

        // Assert
        Assert.Equal(10, strategy.WarmupEndTime);
    }

    [Fact(DisplayName = "Constructor should leave WarmupEndTime as null when it is not provided.")]
    public void Constructor_WithoutWarmupTime_LeavesWarmupPropertyNull()
    {
        // Arrange & Act
        var strategy = new AbsoluteTimeRunStrategy(100);

        // Assert
        Assert.Null(strategy.WarmupEndTime);
    }


    [Theory(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a non-positive stop time.")]
    [InlineData(0)]
    [InlineData(-100)]
    public void Constructor_NonPositiveStopTime_ThrowsArgumentOutOfRangeException(long invalidTime)
    {
        Assert.Throws<ArgumentOutOfRangeException>("stopTime", () => new AbsoluteTimeRunStrategy(invalidTime));
    }

    [Fact(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a negative warm-up time.")]
    public void Constructor_NegativeWarmupTime_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new AbsoluteTimeRunStrategy(100, -10));
    }

    [Theory(DisplayName = "Constructor should throw if warm-up time is not less than stop time.")]
    [InlineData(100, 100)]
    [InlineData(5, 20)]
    public void Constructor_WarmupEndTimeLongerThanStopTime_ThrowsArgumentOutOfRangeException(long stopTime, long invalidWarmupTime)
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new AbsoluteTimeRunStrategy(stopTime, invalidWarmupTime));
    }

    [Theory(DisplayName = "ShouldContinue should return true if clock time is less than stop time.")]
    [InlineData(0)]
    [InlineData(99)]
    public void ShouldContinue_TimeIsBeforeStopTime_ReturnsTrue(long clockTime)
    {
        // Arrange
        var context = new TestRunContext(null!) { ClockTime = clockTime };
        var strategy = new AbsoluteTimeRunStrategy(100);

        // Act
        bool result = strategy.ShouldContinue(context);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "ShouldContinue should return false if clock time is at or after stop time.")]
    [InlineData(100)]
    [InlineData(101)]
    public void ShouldContinue_TimeIsAtOrAfterStopTime_ReturnsFalse(long clockTime)
    {
        // Arrange
        var context = new TestRunContext(null!) { ClockTime = clockTime };
        var strategy = new AbsoluteTimeRunStrategy(100);

        // Act
        bool result = strategy.ShouldContinue(context);

        // Assert
        Assert.False(result);
    }
}
