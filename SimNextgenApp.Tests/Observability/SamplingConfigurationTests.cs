using SimNextgenApp.Observability.Sampling;
using System.Diagnostics;

namespace SimNextgenApp.Tests.Observability;

public class SamplingConfigurationTests
{
    [Fact(DisplayName = "AlwaysOn should create configuration with 100% sampling rate.")]
    public void AlwaysOn_CreatesAlwaysOnConfiguration()
    {
        // Act
        var config = SamplingConfiguration.AlwaysOn();

        // Assert
        Assert.Equal(SamplingStrategy.AlwaysOn, config.Strategy);
        Assert.Equal(1.0, config.SamplingRate);
        Assert.True(config.IsEnabled);
    }

    [Fact(DisplayName = "AlwaysOff should create configuration with 0% sampling rate.")]
    public void AlwaysOff_CreatesAlwaysOffConfiguration()
    {
        // Act
        var config = SamplingConfiguration.AlwaysOff();

        // Assert
        Assert.Equal(SamplingStrategy.AlwaysOff, config.Strategy);
        Assert.Equal(0.0, config.SamplingRate);
        Assert.False(config.IsEnabled);
    }

    [Fact(DisplayName = "Random should create configuration with specified sampling rate.")]
    public void Random_CreatesRandomConfiguration()
    {
        // Act
        var config = SamplingConfiguration.Random(0.5);

        // Assert
        Assert.Equal(SamplingStrategy.Random, config.Strategy);
        Assert.Equal(0.5, config.SamplingRate);
        Assert.True(config.IsEnabled);
    }

    [Theory(DisplayName = "Random should throw for invalid sampling rate.")]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Random_ThrowsForInvalidRate(double rate)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SamplingConfiguration.Random(rate));
    }

    [Fact(DisplayName = "ParentBased should create configuration with specified fallback rate.")]
    public void ParentBased_CreatesParentBasedConfiguration()
    {
        // Act
        var config = SamplingConfiguration.ParentBased(0.25);

        // Assert
        Assert.Equal(SamplingStrategy.ParentBased, config.Strategy);
        Assert.Equal(0.25, config.SamplingRate);
        Assert.True(config.IsEnabled);
    }

    [Theory(DisplayName = "ParentBased should throw for invalid fallback rate.")]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void ParentBased_ThrowsForInvalidFallbackRate(double rate)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SamplingConfiguration.ParentBased(rate));
    }

    [Fact(DisplayName = "ParentBased should default fallback rate to 100%.")]
    public void ParentBased_DefaultsFallbackRateToOne()
    {
        // Act
        var config = SamplingConfiguration.ParentBased();

        // Assert
        Assert.Equal(1.0, config.SamplingRate);
    }

    [Fact(DisplayName = "ShouldSample with AlwaysOn should always return true.")]
    public void ShouldSample_AlwaysOn_ReturnsTrue()
    {
        // Arrange
        var config = SamplingConfiguration.AlwaysOn();

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.True(config.ShouldSample());
        }
    }

    [Fact(DisplayName = "ShouldSample with AlwaysOff should always return false.")]
    public void ShouldSample_AlwaysOff_ReturnsFalse()
    {
        // Arrange
        var config = SamplingConfiguration.AlwaysOff();

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.False(config.ShouldSample());
        }
    }

    [Fact(DisplayName = "ShouldSample with Random should respect configured sampling rate.")]
    public void ShouldSample_Random_RespectsRate()
    {
        // Arrange - 50% sampling rate
        var config = SamplingConfiguration.Random(0.5);
        int sampleCount = 0;
        int totalSamples = 10000;

        // Act
        for (int i = 0; i < totalSamples; i++)
        {
            if (config.ShouldSample())
            {
                sampleCount++;
            }
        }

        // Assert - Allow 5% tolerance (should be around 5000 +/- 500)
        double actualRate = (double)sampleCount / totalSamples;
        Assert.InRange(actualRate, 0.45, 0.55);
    }

    [Fact(DisplayName = "ShouldSample with Random 0% rate should always return false.")]
    public void ShouldSample_Random_ZeroRate_AlwaysReturnsFalse()
    {
        // Arrange
        var config = SamplingConfiguration.Random(0.0);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.False(config.ShouldSample());
        }
    }

    [Fact(DisplayName = "ShouldSample with Random 100% rate should always return true.")]
    public void ShouldSample_Random_FullRate_AlwaysReturnsTrue()
    {
        // Arrange
        var config = SamplingConfiguration.Random(1.0);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            Assert.True(config.ShouldSample());
        }
    }

    [Fact(DisplayName = "ShouldSample with ParentBased should follow recorded parent decision.")]
    public void ShouldSample_ParentBased_WithRecordedParent_ReturnsTrue()
    {
        // Arrange
        var config = SamplingConfiguration.ParentBased(0.0); // Fallback rate is 0
        var activitySource = new ActivitySource("TestSource");

        // Create a listener to enable activity creation
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        // Create a parent activity that is recorded
        using var parent = activitySource.StartActivity("ParentActivity", ActivityKind.Internal);
        Assert.NotNull(parent);

        // Create a child activity
        using var child = activitySource.StartActivity("ChildActivity", ActivityKind.Internal);
        Assert.NotNull(child);

        // Act & Assert - Should follow parent's decision (recorded)
        Assert.True(config.ShouldSample());
    }

    [Fact(DisplayName = "ShouldSample with ParentBased without parent should use fallback rate.")]
    public void ShouldSample_ParentBased_WithoutParent_UsesFallback()
    {
        // Arrange - No parent activity
        var config = SamplingConfiguration.ParentBased(1.0); // Fallback rate is 100%

        // Act & Assert - Should use fallback rate
        Assert.True(config.ShouldSample());
    }

    [Fact(DisplayName = "ToString should return readable description of sampling strategy.")]
    public void ToString_ReturnsReadableDescription()
    {
        // Arrange & Act & Assert
        Assert.Equal("AlwaysOn (100%)", SamplingConfiguration.AlwaysOn().ToString());
        Assert.Equal("AlwaysOff (0%)", SamplingConfiguration.AlwaysOff().ToString());
        Assert.Contains("Random", SamplingConfiguration.Random(0.5).ToString());
        Assert.Contains("50%", SamplingConfiguration.Random(0.5).ToString());
        Assert.Contains("ParentBased", SamplingConfiguration.ParentBased(0.25).ToString());
        Assert.Contains("25%", SamplingConfiguration.ParentBased(0.25).ToString());
    }

    [Fact(DisplayName = "IsEnabled should reflect whether sampling is active.")]
    public void IsEnabled_ReflectsStrategy()
    {
        // Assert
        Assert.True(SamplingConfiguration.AlwaysOn().IsEnabled);
        Assert.False(SamplingConfiguration.AlwaysOff().IsEnabled);
        Assert.True(SamplingConfiguration.Random(0.5).IsEnabled);
        Assert.True(SamplingConfiguration.ParentBased(0.5).IsEnabled);
    }

    [Fact(DisplayName = "ShouldSample with ParentBased before creating child should check current activity, not grandparent.")]
    public void ShouldSample_ParentBased_BeforeCreatingChild_ChecksCurrentActivity()
    {
        // Arrange
        var config = SamplingConfiguration.ParentBased(0.0); // Fallback rate is 0
        var activitySource = new ActivitySource("TestSource");

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        // Create a parent activity (this will be Activity.Current)
        using var parent = activitySource.StartActivity("ParentActivity", ActivityKind.Internal);
        Assert.NotNull(parent);
        Assert.True(parent.Recorded);

        // Act - Before creating child, check if we should sample
        // This should check Activity.Current (parent), not Activity.Current.Parent (grandparent/null)
        bool shouldSample = config.ShouldSample();

        // Assert - Should follow current activity's decision (recorded = true)
        Assert.True(shouldSample);
    }

    [Fact(DisplayName = "ShouldSample with ParentBased should respect non-recorded parent decision.")]
    public void ShouldSample_ParentBased_WithNonRecordedParent_ReturnsFalse()
    {
        // Arrange
        var config = SamplingConfiguration.ParentBased(1.0); // Fallback rate is 100%
        var activitySource = new ActivitySource("TestSource");

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.PropagationData // Not recorded
        };
        ActivitySource.AddActivityListener(listener);

        // Create a parent activity that is NOT recorded
        using var parent = activitySource.StartActivity("ParentActivity", ActivityKind.Internal);
        Assert.NotNull(parent);
        Assert.False(parent.Recorded);

        // Act - Check if we should sample before creating child
        bool shouldSample = config.ShouldSample();

        // Assert - Should follow current activity's decision (recorded = false)
        Assert.False(shouldSample);
    }
}
