using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;

namespace SimNextgenApp.Tests.Core.Strategies;

public class ConditionalRunStrategyTests
{
    [Fact(DisplayName = "Constructor should throw ArgumentNullException if the continue condition is null.")]
    public void Constructor_NullCondition_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("continueCondition", () => new ConditionalRunStrategy(null!));
    }

    [Fact(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a negative warm-up time.")]
    public void Constructor_NegativeWarmupTime_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new ConditionalRunStrategy(ctx => true, -10.0));
    }

    [Theory(DisplayName = "Constructor should set WarmupEndTime when a valid non-negative value is provided.")]
    [InlineData(0.0)]
    [InlineData(100.0)]
    public void Constructor_ValidWarmupTime_SetsWarmupProperty(double warmupTime)
    {
        // Arrange
        var strategy = new ConditionalRunStrategy(ctx => true, warmupTime);

        // Act & Assert
        Assert.Equal(warmupTime, strategy.WarmupEndTime);
    }

    [Fact(DisplayName = "Constructor should leave WarmupEndTime as null when it is not provided.")]
    public void Constructor_NullWarmupTime_SetsWarmupPropertyToNull()
    {
        // Arrange & Act
        var strategy = new ConditionalRunStrategy(ctx => true, warmupEndTime: null);

        // Assert
        Assert.Null(strategy.WarmupEndTime);
    }

    [Theory(DisplayName = "ShouldContinue should return true when the provided condition evaluates to true.")]
    [InlineData(0)] // Event count is less than 10
    [InlineData(9)] // Event count is less than 10
    public void ShouldContinue_WhenConditionIsTrue_ReturnsTrue(long executedEventCount)
    {
        // Arrange
        // The condition is to continue as long as the event count is less than 10.
        var strategy = new ConditionalRunStrategy(ctx => ctx.ExecutedEventCount < 10);

        var mockContext = new Mock<IRunContext>();
        mockContext.SetupGet(c => c.ExecutedEventCount).Returns(executedEventCount);

        // Act
        bool result = strategy.ShouldContinue(mockContext.Object);

        // Assert
        Assert.True(result);
    }

    [Theory(DisplayName = "ShouldContinue should return false when the provided condition evaluates to false.")]
    [InlineData(10)]    // Event count is not less than 10
    [InlineData(15)]    // Event count is not less than 10
    public void ShouldContinue_WhenConditionIsFalse_ReturnsFalse(long executedEventCount)
    {
        // Arrange
        // The condition is to continue as long as the event count is less than 10.
        var strategy = new ConditionalRunStrategy(ctx => ctx.ExecutedEventCount < 10);

        var mockContext = new Mock<IRunContext>();
        mockContext.SetupGet(c => c.ExecutedEventCount).Returns(executedEventCount);

        // Act
        bool result = strategy.ShouldContinue(mockContext.Object);

        // Assert
        Assert.False(result);
    }
}
