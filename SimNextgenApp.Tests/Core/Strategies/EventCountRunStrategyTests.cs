using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Core.Strategies;

public class EventCountRunStrategyTests
{
    [Fact(DisplayName = "Constructor should set properties correctly when valid arguments are provided.")]
    public void Constructor_WithValidArguments_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var strategy = new EventCountRunStrategy(maxEventCount: 1000, warmupEndTime: 100.0);

        // Assert
        Assert.Equal(100.0, strategy.WarmupEndTime);
    }

    [Fact(DisplayName = "Constructor should leave WarmupEndTime as null when it is not provided.")]
    public void Constructor_WithoutWarmupTime_LeavesWarmupPropertyNull()
    {
        // Arrange & Act
        var strategy = new EventCountRunStrategy(maxEventCount: 1000);

        // Assert
        Assert.Null(strategy.WarmupEndTime);
    }

    [Theory(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a non-positive event count.")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsArgumentOutOfRangeException_ForNonPositiveMaxEventCount(long invalidMaxEventCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>("maxEventCount", () => new EventCountRunStrategy(invalidMaxEventCount));
    }

    [Fact(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a negative warm-up time.")]
    public void Constructor_NegativeWarmupEndTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        long maxEventCount = 10;
        double invalidWarmup = -5.0;

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new EventCountRunStrategy(maxEventCount, invalidWarmup));
    }

    [Theory(DisplayName = "ShouldContinue should return true when the executed event count is less than the maximum.")]
    [InlineData(0)]   // Start of simulation
    [InlineData(998)] // Middle of simulation
    [InlineData(999)] // Just before the limit
    public void ShouldContinue_EventCountIsLessThanMax_ReturnsTrue(long executedEventCount)
    {
        // Arrange
        var strategy = new EventCountRunStrategy(maxEventCount: 1000);
        var mockContext = new Mock<IRunContext>();
        mockContext.SetupGet(c => c.ExecutedEventCount).Returns(executedEventCount);

        // Act
        bool result = strategy.ShouldContinue(mockContext.Object);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "ShouldContinue should return false when the executed event count is at or over the maximum.")]
    [InlineData(1000)] // Exactly at the limit
    [InlineData(1001)] // Past the limit
    public void ShouldContinue_EventCountIsAtOrOverMax_ReturnsFalse(long executedEventCount)
    {
        // Arrange
        var strategy = new EventCountRunStrategy(maxEventCount: 1000);
        var mockContext = new Mock<IRunContext>();
        mockContext.SetupGet(c => c.ExecutedEventCount).Returns(executedEventCount);

        // Act
        bool result = strategy.ShouldContinue(mockContext.Object);

        // Assert
        Assert.False(result);
    }
}
