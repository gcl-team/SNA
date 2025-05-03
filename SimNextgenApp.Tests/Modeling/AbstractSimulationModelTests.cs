using SimNextgenApp.Core;
using SimNextgenApp.Modeling;
using Xunit;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Diagnostics; 

namespace SimulationNextgenApp.Tests.Modeling;

public class AbstractSimulationModelTests
{
    [Fact]
    public void Constructor_AssignsNameCorrectly()
    {
        string expectedName = "Test Model 1";

        var model = new ConcreteTestModel(expectedName);

        Assert.Equal(expectedName, model.Name);
    }

    [Fact]
    public void Constructor_InitializesMetadata()
    {
        var model = new ConcreteTestModel("Test Model 2");

        Assert.NotNull(model.Metadata);
        Assert.IsAssignableFrom<IDictionary<string, object>>(model.Metadata);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenNameIsNullOrWhitespace(string invalidName)
    {
        var ex = Assert.Throws<ArgumentException>(() => new ConcreteTestModel(invalidName));

        Assert.Contains("Model name cannot be null or whitespace", ex.Message);
        Assert.Equal("name", ex.ParamName); 
    }

    [Fact]
    public void Constructor_AssignsUniqueIds()
    {
        var model1 = new ConcreteTestModel("M1");
        var model2 = new ConcreteTestModel("M2");

        Assert.NotEqual(model1.Id, model2.Id);
        Assert.True(model2.Id > model1.Id, "Subsequent ID should be greater");
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        var model = new ConcreteTestModel("MyFormatTest");
        string expected = $"MyFormatTest#{model.Id}";

        string actual = model.ToString();

        Assert.Equal(expected, actual);
    }
}

// A minimal concrete implementation for testing
internal class ConcreteTestModel : AbstractSimulationModel
{
    public ConcreteTestModel(string name) : base(name) { }

    public override void Initialize(IScheduler scheduler)
    {
        Debug.WriteLine($"TestModel {Name} Initialized.");
    }

    public override void WarmedUp(double simulationTime)
    {
        Debug.WriteLine($"TestModel {Name} WarmedUp at {simulationTime}.");
    }
}