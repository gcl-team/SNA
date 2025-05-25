using SimNextgenApp.Configurations;

namespace SimNextgenApp.Tests.Configurations;

public class GeneratorStaticConfigTests
{
    private readonly Func<Random, TimeSpan> _validInterArrivalTime = (rand) => TimeSpan.FromSeconds(1);
    private readonly Func<Random, DummyLoadType> _validLoadFactory = (rand) => new DummyLoadType();

    [Fact]
    public void Constructor_WithValidArguments_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var config = new GeneratorStaticConfig<DummyLoadType>(
            _validInterArrivalTime,
            _validLoadFactory
        );

        // Assert
        Assert.Same(_validInterArrivalTime, config.InterArrivalTime); // Check for reference equality for delegates
        Assert.Same(_validLoadFactory, config.LoadFactory);
        Assert.True(config.IsSkippingFirst); // Default value
    }

    [Fact]
    public void Constructor_NullInterArrivalTime_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new GeneratorStaticConfig<DummyLoadType>(
                null!, // Passing null intentionally
                _validLoadFactory
            )
        );

        Assert.Equal("interArrivalTime", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullLoadFactory_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new GeneratorStaticConfig<DummyLoadType>(
                _validInterArrivalTime,
                null! // Passing null intentionally
            )
        );

        Assert.Equal("loadFactory", ex.ParamName);
    }

    [Fact]
    public void IsSkippingFirst_CanBeSetViaObjectInitializer()
    {
        // Arrange & Act
        var config = new GeneratorStaticConfig<DummyLoadType>(
            _validInterArrivalTime,
            _validLoadFactory
        ) { IsSkippingFirst = false };

        // Assert
        Assert.False(config.IsSkippingFirst);
    }
}