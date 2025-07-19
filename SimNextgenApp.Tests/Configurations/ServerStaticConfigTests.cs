using SimNextgenApp.Configurations;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Configurations;

public class ServerStaticConfigTests
{
    private readonly Func<DummyLoadType, Random, TimeSpan> _validServiceTime = 
        (load, rand) => TimeSpan.FromSeconds(5);

    [Fact(DisplayName = "Constructor with a valid service time should initialize properties correctly.")]
    public void Constructor_WithValidServiceTime_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var config = new ServerStaticConfig<DummyLoadType>(_validServiceTime);

        // Assert
        Assert.Same(_validServiceTime, config.ServiceTime);
        Assert.Equal(int.MaxValue, config.Capacity); // Verify the default value
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException if service time is null.")]
    public void Constructor_NullServiceTime_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => 
            new ServerStaticConfig<DummyLoadType>(null!)
        );
        Assert.Equal("serviceTime", ex.ParamName);
    }

    [Fact(DisplayName = "Capacity can be set to a valid value via an object initializer.")]
    public void Capacity_CanBeSetViaObjectInitializer()
    {
        // Arrange & Act
        var config = new ServerStaticConfig<DummyLoadType>(_validServiceTime)
        {
            Capacity = 10
        };

        // Assert
        Assert.Equal(10, config.Capacity);
        Assert.Same(_validServiceTime, config.ServiceTime);
    }

    [Fact(DisplayName = "Setting Capacity to zero should be allowed.")]
    public void Capacity_SetToZero_IsAllowed()
    {
        // Arrange & Act
        var config = new ServerStaticConfig<DummyLoadType>(_validServiceTime)
        {
            Capacity = 0
        };

        // Assert
        Assert.Equal(0, config.Capacity);
    }

    [Fact(DisplayName = "Setting Capacity to a negative value should throw ArgumentOutOfRangeException.")]
    public void Capacity_SetToNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var act = () => new ServerStaticConfig<DummyLoadType>(_validServiceTime)
        {
            Capacity = -1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>("Capacity", act);
    }

    [Fact(DisplayName = "Two ServerStaticConfig instances with identical values should be considered equal.")]
    public void Records_WithIdenticalValues_AreEqual()
    {
        // Arrange
        var config1 = new ServerStaticConfig<DummyLoadType>(_validServiceTime)
        {
            Capacity = 50
        };

        var config2 = new ServerStaticConfig<DummyLoadType>(_validServiceTime)
        {
            Capacity = 50
        };

        // Act & Assert
        Assert.Equal(config1, config2);
        Assert.True(config1 == config2);
        Assert.False(object.ReferenceEquals(config1, config2));
    }
}