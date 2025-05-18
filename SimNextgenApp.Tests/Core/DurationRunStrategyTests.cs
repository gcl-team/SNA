using SimNextgenApp.Core;

namespace SimulationNextgenApp.Tests.Core;

public class DurationRunStrategyTests
{
    [Fact]
    public void Constructor_ValidInputs_SetsProperties()
    {
        var strategy = new DurationRunStrategy(100.0, 10.0);
        Assert.Equal(10.0, strategy.WarmupEndTime);
    }

    [Fact]
    public void Constructor_ValidInputs_SetsProperties_NoWarmup()
    {
        var strategy = new DurationRunStrategy(100.0);
        Assert.Null(strategy.WarmupEndTime);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    public void Constructor_Throws_When_RunDuration_Is_Not_Positive(double invalidDuration)
    {
        Assert.Throws<ArgumentOutOfRangeException>("runDuration", () => new DurationRunStrategy(invalidDuration));
    }

    [Fact]
    public void Constructor_Throws_When_WarmupDuration_Is_Negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupDuration", () => new DurationRunStrategy(100.0, -10.0));
    }

    [Fact]
    public void Constructor_Throws_When_WarmupDuration_Is_Longer_Than_RunDuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupDuration", () => new DurationRunStrategy(5.0, 20.0));
    }
    
    [Theory]
    [InlineData(100.0, 100.0)]
    [InlineData(100.0, 101.0)]
    public void Constructor_Throws_When_WarmupDuration_Is_Not_Less_Than_RunDuration(double runDuration, double invalidWarmup)
    {
        Assert.Throws<ArgumentOutOfRangeException>("warmupDuration", () => new DurationRunStrategy(runDuration, invalidWarmup));
    }

    [Theory]
    [InlineData(0.0, 100.0, true)]
    [InlineData(99.9, 100.0, true)]
    [InlineData(100.0, 100.0, false)]
    [InlineData(100.1, 100.0, false)]
    public void ShouldContinue_ReturnsCorrectValueBasedOnClockTime(double clockTime, double runDuration, bool expectedResult)
    {
        // Arrange
        var context = new TestRunContext { ClockTime = clockTime };
        var strategy = new DurationRunStrategy(runDuration);

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
