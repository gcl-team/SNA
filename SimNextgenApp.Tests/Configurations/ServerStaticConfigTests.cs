using SimNextgenApp.Configurations;

namespace SimNextgenApp.Tests.Configurations;

public class ServerStaticConfigTests
{
    private readonly Func<DummyLoadType, Random, TimeSpan> _validServiceTime = 
        (load, rand) => TimeSpan.FromSeconds(5);

    [Fact]
    public void Constructor_WithValidServiceTime_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var config = new ServerStaticConfig<DummyLoadType>(_validServiceTime);

        // Assert
        Assert.Same(_validServiceTime, config.ServiceTime);
        Assert.Equal(int.MaxValue, config.Capacity); // Check default
    }

    [Fact]
    public void Constructor_NullServiceTime_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => 
            new ServerStaticConfig<DummyLoadType>(null!)
        );
        Assert.Equal("serviceTime", ex.ParamName);
    }

    [Fact]
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
}