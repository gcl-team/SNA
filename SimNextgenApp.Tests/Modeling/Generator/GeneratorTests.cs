using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;
using SimNextgenApp.Modeling.Generator;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Modeling.Generator;

public class GeneratorTests
{
    private const string DefaultGeneratorName = "TestGen";
    private const int DefaultSeed = 123;

    private Mock<IScheduler> _mockScheduler;
    private Mock<IRunContext> _mockEngine;

    private GeneratorStaticConfig<DummyLoad> _defaultConfig;
    private Func<Random, TimeSpan> _defaultInterArrivalTimeFunc;
    private Func<Random, DummyLoad> _defaultLoadFactoryFunc;
    private int _loadFactoryCallCount;
    private double _currentTestTime = 0.0;

    public GeneratorTests()
    {
        _mockScheduler = new Mock<IScheduler>();

        _mockEngine = new Mock<IRunContext>();
        _mockEngine.SetupGet(e => e.ClockTime).Returns(() => _currentTestTime);
        _mockEngine.Setup(e => e.Scheduler).Returns(_mockScheduler.Object);

        _defaultInterArrivalTimeFunc = (rand) => TimeSpan.FromSeconds(5); // Fixed for predictability
        _loadFactoryCallCount = 0;
        _defaultLoadFactoryFunc = (rand) =>
        {
            _loadFactoryCallCount++;
            return new DummyLoad($"Load{_loadFactoryCallCount}");
        };
        _defaultConfig = new GeneratorStaticConfig<DummyLoad>(
            interArrivalTime: _defaultInterArrivalTimeFunc,
            loadFactory: _defaultLoadFactoryFunc
        );
    }

    private Generator<DummyLoad> CreateGenerator(
        GeneratorStaticConfig<DummyLoad>? config = null,
        int seed = DefaultSeed,
        string name = DefaultGeneratorName)
    {
        _loadFactoryCallCount = 0; // Reset for each generator creation
        return new Generator<DummyLoad>(config ?? _defaultConfig, seed, name, NullLoggerFactory.Instance);
    }

    [Fact(DisplayName = "Constructor should initialize properties to their default state.")]
    public void Constructor_WithValidConfig_InitializesCorrectly()
    {
        // Arrange
        var generator = CreateGenerator();

        // Assert
        Assert.Equal(DefaultGeneratorName, generator.Name);
        Assert.False(generator.IsActive);
        Assert.Equal(0, generator.LoadsGeneratedCount);
        Assert.Null(generator.StartTime);
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException if config is null.")]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        GeneratorStaticConfig<DummyLoad> nullConfig = null!;
        int seed = DefaultSeed;
        string name = DefaultGeneratorName;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new Generator<DummyLoad>(nullConfig, seed, name, NullLoggerFactory.Instance)
        );
        Assert.Equal("config", ex.ParamName);
    }

    [Fact(DisplayName = "Initialize should throw ArgumentNullException if context is null.")]
    public void Initialize_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = CreateGenerator();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => generator.Initialize(null!));
        Assert.Equal("engineContext", ex.ParamName);
    }

    [Fact(DisplayName = "Initialize should schedule a GeneratorStartEvent at time zero.")]
    public void Initialize_SchedulesStartEventAtTimeZero()
    {
        // Arrange
        var generator = CreateGenerator();

        // Act
        generator.Initialize(_mockEngine.Object);

        // Assert
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<AbstractEvent>(), 0.0), Times.Once);
    }

    [Fact(DisplayName = "ScheduleStartGenerating should schedule a GeneratorStartEvent at the current clock time.")]
    public void ScheduleStartGenerating_SchedulesStartEvent()
    {
        // Arrange
        var generator = CreateGenerator();
        _currentTestTime = 10.0;

        // Act
        generator.ScheduleStartGenerating(_mockEngine.Object);

        // Assert
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<GeneratorStartEvent<DummyLoad>>(), 10.0), Times.Once);
    }

    [Fact(DisplayName = "ScheduleStartGenerating should throw ArgumentNullException for a null context.")]
    public void ScheduleStartGenerating_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = CreateGenerator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>("engineContext", () => generator.ScheduleStartGenerating(null!));
    }

    [Fact(DisplayName = "ScheduleStopGenerating should schedule a GeneratorStopEvent at the current clock time.")]
    public void ScheduleStopGenerating_SchedulesStopEvent()
    {
        // Arrange
        var generator = CreateGenerator();
        _currentTestTime = 20.0;

        // Act
        generator.ScheduleStopGenerating(_mockEngine.Object);

        // Assert
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<GeneratorStopEvent<DummyLoad>>(), 20.0), Times.Once);
    }

    [Fact(DisplayName = "Executing GeneratorStartEvent should activate and schedule immediate arrival (not skipping first).")]
    public void GeneratorStartEvent_Execute_WhenNotSkippingFirst_SchedulesImmediateArrival()
    {
        // Arrange
        var config = _defaultConfig with { IsSkippingFirst = false };
        var generator = CreateGenerator(config);
        var startEvent = new GeneratorStartEvent<DummyLoad>(generator);
        _currentTestTime = 10.0;

        // Act
        startEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.True(generator.IsActive);
        // The next event is scheduled for the current time
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<GeneratorArriveEvent<DummyLoad>>(), 10.0), Times.Once);
    }

    [Fact(DisplayName = "Executing GeneratorStartEvent should do nothing if generator is already active.")]
    public void GeneratorStartEvent_Execute_WhenAlreadyActive_DoesNothing()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.HandleActivation(_mockEngine.Object); // Manually activate
        _mockScheduler.Invocations.Clear(); // Clear the schedule call from HandleActivation

        var startEvent = new GeneratorStartEvent<DummyLoad>(generator);

        // Act
        startEvent.Execute(_mockEngine.Object);

        // Assert
        // Verify no *new* arrival event was scheduled
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<GeneratorArriveEvent<DummyLoad>>(), It.IsAny<double>()), Times.Never);
    }

    [Fact(DisplayName = "Executing GeneratorStopEvent should deactivate the generator.")]
    public void GeneratorStopEvent_Execute_DeactivatesGenerator()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.HandleActivation(_mockEngine.Object); // Manually activate
        Assert.True(generator.IsActive);

        var stopEvent = new GeneratorStopEvent<DummyLoad>(generator);

        // Act
        stopEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.False(generator.IsActive);
    }

    [Fact(DisplayName = "Executing GeneratorArriveEvent should generate a load and schedule the next arrival.")]
    public void GeneratorArriveEvent_Execute_WhenActive_GeneratesLoadAndSchedulesNext()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.HandleActivation(_mockEngine.Object); // Must be active
        _mockScheduler.Invocations.Clear();

        var arriveEvent = new GeneratorArriveEvent<DummyLoad>(generator);
        _currentTestTime = 20.0;

        DummyLoad? generatedLoad = null;
        generator.LoadGenerated += (load, time) => generatedLoad = load;

        // Act
        arriveEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.Equal(1, generator.LoadsGeneratedCount);
        Assert.NotNull(generatedLoad);
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<GeneratorArriveEvent<DummyLoad>>(), TimeSpan.FromSeconds(5)), Times.Once);
    }

    [Fact(DisplayName = "Executing GeneratorArriveEvent should do nothing if generator is inactive.")]
    public void GeneratorArriveEvent_Execute_WhenInactive_DoesNothing()
    {
        // Arrange
        var generator = CreateGenerator(); // Is inactive by default
        var arriveEvent = new GeneratorArriveEvent<DummyLoad>(generator);

        // Act
        arriveEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.Equal(0, generator.LoadsGeneratedCount);
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<double>()), Times.Never);
    }

    [Fact(DisplayName = "WarmedUp should reset StartTime and LoadsGeneratedCount while remaining active.")]
    public void WarmedUp_ResetsStatistics()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockEngine.Object);
        _mockEngine.SetupGet(c => c.ClockTime).Returns(30.0);
        generator.HandleActivation(_mockEngine.Object);

        // Act
        generator.WarmedUp(simulationTime: 100.0);

        // Assert
        Assert.Equal(100.0, generator.StartTime);
        Assert.Equal(0, generator.LoadsGeneratedCount);
        Assert.True(generator.IsActive); // WarmedUp should not change IsActive state
    }
}