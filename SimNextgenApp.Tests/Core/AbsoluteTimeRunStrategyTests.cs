using SimNextgenApp.Core;

namespace SimulationNextgenApp.Tests.Core;

public class AbsoluteTimeRunStrategyTests
{
    [Fact]
    public void Constructor_ValidInputs_SetsProperties()
    {
        var strategy = new AbsoluteTimeRunStrategy(100.0, 10.0);
        Assert.Equal(10.0, strategy.WarmupEndTime);
    }

    [Fact]
    public void Constructor_ValidInputs_SetsProperties_NoWarmup()
    {
        var strategy = new AbsoluteTimeRunStrategy(100.0);
        Assert.Null(strategy.WarmupEndTime);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    public void Constructor_Throws_When_StopTime_Is_Not_Positive(double invalidTime)
    {
        Assert.Throws<ArgumentOutOfRangeException>("stopTime", () => new AbsoluteTimeRunStrategy(invalidTime));
    }

    [Fact]
    public void Constructor_Throws_When_WarmupEndTime_Is_Negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new AbsoluteTimeRunStrategy(100.0, -10.0));
    }

    [Fact]
    public void Constructor_Throws_When_WarmupEndTime_Is_Longer_Than_StopTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupEndTime", () => new AbsoluteTimeRunStrategy(5.0, 20.0));
    }

    [Theory]
    [InlineData(0.0, 100.0, true)]
    [InlineData(99.9, 100.0, true)]
    [InlineData(100.0, 100.0, false)]
    [InlineData(100.1, 100.0, false)]
    public void ShouldContinue_ReturnsCorrectValueBasedOnClockTime(double clockTime, double stopTime, bool expectedResult)
    {
        // Arrange
        var context = new TestRunContext { ClockTime = clockTime };
        var strategy = new AbsoluteTimeRunStrategy(stopTime);

        // Act
        bool actualResult = strategy.ShouldContinue(context);

        // Assert
        Assert.Equal(expectedResult, actualResult);
    }

    private class TestRunContext : IRunContext
    {
        public double ClockTime { get; set; }
        public long ExecutedEventCount { get; set; }
    }
}
