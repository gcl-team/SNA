using System.Collections.Immutable;
using SimNextgenApp.Configurations; 

namespace SimNextgenApp.Tests.Configurations;

public class MultiStageProcessServerStaticConfigTests
{
    [Fact]
    public void Constructor_NullStageConfigs_ThrowsArgumentNullException()
    {
        // Arrange
        IEnumerable<ServerStaticConfig<DummyLoadType>> nullStageConfigs = null!;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MultiStageProcessServerStaticConfig<DummyLoadType>(nullStageConfigs)
        );
        Assert.Equal("stageConfigs", ex.ParamName);
    }

    [Fact]
    public void Constructor_EmptyStageConfigs_ThrowsArgumentException()
    {
        // Arrange
        var emptyStageConfigs = Enumerable.Empty<ServerStaticConfig<DummyLoadType>>();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new MultiStageProcessServerStaticConfig<DummyLoadType>(emptyStageConfigs)
        );
        Assert.Equal("stageConfigs", ex.ParamName);
        Assert.Contains("At least one stage configuration must be provided.", ex.Message);
    }

    [Fact]
    public void Constructor_StageConfigsContainingNullElement_ThrowsArgumentException()
    {
        // Arrange
        var stageConfigsWithNull = new List<ServerStaticConfig<DummyLoadType>>
        {
            CreateValidStageConfig(1),
            null!,
            CreateValidStageConfig(2)
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new MultiStageProcessServerStaticConfig<DummyLoadType>(stageConfigsWithNull)
        );
        Assert.Equal("stageConfigs", ex.ParamName);
        Assert.Contains("Stage configurations cannot contain null elements.", ex.Message);
    }

    [Fact]
    public void Constructor_WithSingleValidStage_InitializesStageConfigsCorrectly()
    {
        // Arrange
        var stage1 = CreateValidStageConfig(1);
        var inputStages = new List<ServerStaticConfig<DummyLoadType>> { stage1 };

        // Act
        var config = new MultiStageProcessServerStaticConfig<DummyLoadType>(inputStages);

        // Assert
        Assert.NotNull(config.StageConfigs);
        Assert.IsType<ImmutableList<ServerStaticConfig<DummyLoadType>>>(config.StageConfigs, exactMatch: false);
        Assert.Single(config.StageConfigs);
        Assert.Same(stage1, config.StageConfigs[0]);
    }

    [Fact]
    public void Constructor_WithMultipleValidStages_InitializesStageConfigsInCorrectOrder()
    {
        // Arrange
        var stage1 = CreateValidStageConfig(1);
        var stage2 = CreateValidStageConfig(2);
        var stage3 = CreateValidStageConfig(3);
        var inputStages = new List<ServerStaticConfig<DummyLoadType>> { stage1, stage2, stage3 };

        // Act
        var config = new MultiStageProcessServerStaticConfig<DummyLoadType>(inputStages);

        // Assert
        Assert.NotNull(config.StageConfigs);
        Assert.Equal(3, config.StageConfigs.Count);
        Assert.Same(stage1, config.StageConfigs[0]);
        Assert.Same(stage2, config.StageConfigs[1]);
        Assert.Same(stage3, config.StageConfigs[2]);
    }

    private static ServerStaticConfig<DummyLoadType> CreateValidStageConfig(int id = 0)
    {
        return new ServerStaticConfig<DummyLoadType>((load, rand) => TimeSpan.FromSeconds(id + 1));
    }
}