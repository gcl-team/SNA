using SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Core;

public class EventCountRunStrategyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsArgumentOutOfRangeException_ForNonPositiveMaxEventCount(long invalidMaxEventCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>("maxEventCount", () => new EventCountRunStrategy(invalidMaxEventCount));
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeException_ForNegativeWarmupEndTime()
    {
        // Arrange
        long maxEventCount = 10;
        double invalidWarmup = -5.0;

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new EventCountRunStrategy(maxEventCount, invalidWarmup));
    }

    [Theory]
    [InlineData(0, 10, true)]   // 0 executed events, max 10 -> continue
    [InlineData(5, 10, true)]   // 5 executed events, max 10 -> continue
    [InlineData(9, 10, true)]   // 9 executed events, max 10 -> continue
    [InlineData(10, 10, false)] // 10 executed events, max 10 -> stop
    [InlineData(15, 10, false)] // 15 executed events, max 10 -> stop
    public void ShouldContinue_ReturnsExpectedValue(long executedEventCount, long maxEventCount, bool expected)
    {
        // Arrange
        var context = new TestRunContext { ExecutedEventCount = executedEventCount };
        var strategy = new EventCountRunStrategy(maxEventCount);

        // Act
        bool actual = strategy.ShouldContinue(context);

        // Assert
        Assert.Equal(expected, actual);
    }
}
