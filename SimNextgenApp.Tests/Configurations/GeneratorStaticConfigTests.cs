using SimNextgenApp.Configurations;

namespace SimNextgenApp.Tests.Configurations;

public class GeneratorStaticConfigTests
{
    private readonly Func<Random, TimeSpan> _validInterArrivalTime = (rand) => TimeSpan.FromSeconds(1);
    private readonly Func<Random, DummyLoadType> _validLoadFactory = (rand) => new DummyLoadType();

    [Fact(DisplayName = "Constructor with valid arguments should initialise all properties correctly and set default values.")]
    public void Constructor_WithValidArguments_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var config = new GeneratorStaticConfig<DummyLoadType>(
            _validInterArrivalTime,
            _validLoadFactory
        );

        // Assert
        Assert.Same(_validInterArrivalTime, config.InterArrivalTime);
        Assert.Same(_validLoadFactory, config.LoadFactory);
        Assert.True(config.IsSkippingFirst); // Verify the default value
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException if the inter-arrival time delegate is null.")]
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

    [Fact(DisplayName = "Constructor should throw ArgumentNullException if the load factory delegate is null.")]
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

    [Fact(DisplayName = "The 'IsSkippingFirst' property can be set to false via an object initializer.")]
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

    [Fact(DisplayName = "Two GeneratorStaticConfig instances with identical values should be considered equal.")]
    public void Records_WithIdenticalValues_AreEqual()
    {
        // Arrange
        var config1 = new GeneratorStaticConfig<DummyLoadType>(
            _validInterArrivalTime,
            _validLoadFactory
        )
        { IsSkippingFirst = true };

        var config2 = new GeneratorStaticConfig<DummyLoadType>(
            _validInterArrivalTime,
            _validLoadFactory
        )
        { IsSkippingFirst = true };

        // Act & Assert
        Assert.Equal(config1, config2);
        Assert.True(config1 == config2);
        Assert.False(object.ReferenceEquals(config1, config2));
    }
}