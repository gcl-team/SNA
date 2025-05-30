using SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Core;

public class ConditionalRunStrategyTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConditionIsNull()
    {
        Assert.Throws<ArgumentNullException>("continueCondition", () => new ConditionalRunStrategy(null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.0001)]
    public void Constructor_ThrowsArgumentOutOfRangeException_WhenWarmupTimeIsNegative(double warmupTime)
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new ConditionalRunStrategy(ctx => true, warmupTime));
    }

    [Fact]
    public void WarmupEndTime_ReturnsExpectedValue()
    {
        // Arrange
        double warmupTime = 100.0;
        var strategy = new ConditionalRunStrategy(ctx => true, warmupTime);

        // Act & Assert
        Assert.Equal(warmupTime, strategy.WarmupEndTime);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(10, false)]
    [InlineData(15, false)]
    public void ShouldContinue_RespectsProvidedCondition(long executedEvents, bool expectedResult)
    {
        // Arrange: Condition allows up to 9 events only
        var strategy = new ConditionalRunStrategy(ctx => ctx.ExecutedEventCount < 10);
        var context = new TestRunContext(null!) { ExecutedEventCount = executedEvents };

        // Act
        var result = strategy.ShouldContinue(context);

        // Assert
        Assert.Equal(expectedResult, result);
    }
}
