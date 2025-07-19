using SimNextgenApp.Core;
using SimNextgenApp.Modeling;
using System.Diagnostics; 

namespace SimNextgenApp.Tests.Modeling;

public class AbstractSimulationModelTests
{
    [Fact(DisplayName = "Constructor should assign the provided name correctly.")]
    public void Constructor_ValidName_AssignsNameCorrectly()
    {
        string expectedName = "Test Model 1";

        var model = new ConcreteTestModel(expectedName);

        Assert.Equal(expectedName, model.Name);
    }

    [Fact(DisplayName = "Constructor should initialize an empty, non-null Metadata dictionary.")]
    public void Constructor_InitializesMetadata()
    {
        var model = new ConcreteTestModel("Test Model 2");

        Assert.NotNull(model.Metadata);
        Assert.Empty(model.Metadata);
        Assert.IsAssignableFrom<IDictionary<string, object>>(model.Metadata);
    }

    [Theory(DisplayName = "Constructor should throw ArgumentException for a null or whitespace name.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentException_WhenNameIsNullOrWhitespace(string invalidName)
    {
        var ex = Assert.Throws<ArgumentException>(() => new ConcreteTestModel(invalidName));

        Assert.Contains("Model name cannot be null or whitespace", ex.Message);
        Assert.Equal("name", ex.ParamName); 
    }

    [Fact(DisplayName = "Constructor should assign unique and sequential IDs to new instances.")]
    public void Constructor_AssignsUniqueIds()
    {
        var model1 = new ConcreteTestModel("M1");
        var model2 = new ConcreteTestModel("M2");

        Assert.NotEqual(model1.Id, model2.Id);
        Assert.True(model2.Id > model1.Id, "Subsequent ID should be greater");
    }

    [Fact(DisplayName = "ToString should return the name and ID in the format 'Name#Id'.")]
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

    public override void Initialize(IRunContext engineContext)
    {
        Debug.WriteLine($"TestModel {Name} Initialized.");
    }

    public override void WarmedUp(double simulationTime)
    {
        Debug.WriteLine($"TestModel {Name} WarmedUp at {simulationTime}.");
    }
}