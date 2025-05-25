using SimNextgenApp.Configurations;

namespace SimNextgenApp.Tests.Configurations;

public class LoadQueueStaticConfigTests
{
    [Fact]
    public void DefaultConstructor_Capacity_IsIntMaxValue()
    {
        // Arrange & Act
        var config = new LoadQueueStaticConfig();

        // Assert
        Assert.Equal(int.MaxValue, config.Capacity);
    }

    [Fact]
    public void Capacity_CanBeSetViaObjectInitializer()
    {
        // Arrange
        var expectedCapacity = 100;

        // Act
        var config = new LoadQueueStaticConfig { Capacity = expectedCapacity };

        // Assert
        Assert.Equal(expectedCapacity, config.Capacity);
    }
}