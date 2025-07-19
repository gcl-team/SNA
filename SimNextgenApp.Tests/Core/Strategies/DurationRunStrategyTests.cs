using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Core.Strategies;

public class DurationRunStrategyTests
{
    [Fact(DisplayName = "Constructor should set WarmupEndTime when a valid value is provided.")]
    public void Constructor_WithValidWarmupDuration_SetsWarmupProperty()
    {
        // Arrange & Act
        var strategy = new DurationRunStrategy(100.0, 10.0);

        // Assert
        Assert.Equal(10.0, strategy.WarmupEndTime);
    }

    [Fact(DisplayName = "Constructor should leave WarmupEndTime as null when it is not provided.")]
    public void Constructor_WithoutWarmupDuration_LeavesWarmupPropertyNull()
    {
        // Arrange & Act
        var strategy = new DurationRunStrategy(100.0);

        // Assert
        Assert.Null(strategy.WarmupEndTime);
    }

    [Theory(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a non-positive run duration.")]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    public void Constructor_NonPositiveRunDuration_ThrowsArgumentOutOfRangeException(double invalidDuration)
    {
        Assert.Throws<ArgumentOutOfRangeException>("runDuration", () => 
            new DurationRunStrategy(invalidDuration)
        );
    }

    [Fact(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a negative warm-up duration.")]
    public void Constructor_NegativeWarmupDuration_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupDuration", () => 
            new DurationRunStrategy(100.0, -10.0)
        );
    }

    [Theory(DisplayName = "Constructor should throw if warm-up duration is not less than run duration.")]
    [InlineData(100.0, 100.0)]
    [InlineData(99.0, 100.0)]
    public void Constructor_WarmupNotLessThanRunDuration_ThrowsArgumentOutOfRangeException(double runDuration, double invalidWarmup)
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupDuration", () => new DurationRunStrategy(runDuration, invalidWarmup));
    }

    [Theory(DisplayName = "ShouldContinue should return true if clock time is less than run duration.")]
    [InlineData(0.0)]
    [InlineData(99.999)]
    public void ShouldContinue_TimeIsBeforeRunDuration_ReturnsTrue(double clockTime)
    {
        // Arrange
        var mockContext = new Mock<IRunContext>();
        mockContext.SetupGet(c => c.ClockTime).Returns(clockTime);
        var strategy = new DurationRunStrategy(runDuration: 100.0);

        // Act
        bool result = strategy.ShouldContinue(mockContext.Object);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "ShouldContinue should return false if clock time is at or after run duration.")]
    [InlineData(100.0)]
    [InlineData(100.001)]
    public void ShouldContinue_TimeIsAtOrAfterRunDuration_ReturnsFalse(double clockTime)
    {
        // Arrange
        var mockContext = new Mock<IRunContext>();
        mockContext.SetupGet(c => c.ClockTime).Returns(clockTime);
        var strategy = new DurationRunStrategy(runDuration: 100.0);

        // Act
        bool result = strategy.ShouldContinue(mockContext.Object);

        // Assert
        Assert.False(result);
    }
}
