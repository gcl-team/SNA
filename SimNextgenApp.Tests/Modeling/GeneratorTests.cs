using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;
using SimNextgenApp.Modeling;

namespace SimNextgenApp.Tests.Modeling;

public class GeneratorTests
{
    private const string DefaultGeneratorName = "TestGen";
    private const int DefaultSeed = 123;

    private Mock<IScheduler> _mockScheduler;
    private Mock<IRunContext> _mockEngine;

    private GeneratorStaticConfig<DummyLoad> _validConfig;
    private Func<Random, TimeSpan> _defaultInterArrivalTimeFunc;
    private Func<Random, DummyLoad> _defaultLoadFactoryFunc;
    private int _loadFactoryCallCount;
    private double _currentTestTime = 0.0;

    public GeneratorTests()
    {
        _mockScheduler = new Mock<IScheduler>();

        _mockEngine = new Mock<IRunContext>();
        _mockEngine.As<IScheduler>().Setup(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<double>()));
        _mockEngine.SetupGet(e => e.ClockTime).Returns(() => _currentTestTime);
        _mockEngine.Setup(e => e.Scheduler).Returns(_mockScheduler.Object);

        _defaultInterArrivalTimeFunc = (rand) => TimeSpan.FromSeconds(5); // Fixed for predictability
        _loadFactoryCallCount = 0;
        _defaultLoadFactoryFunc = (rand) =>
        {
            _loadFactoryCallCount++;
            return new DummyLoad($"Load{_loadFactoryCallCount}");
        };
        _validConfig = new GeneratorStaticConfig<DummyLoad>(
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
        return new Generator<DummyLoad>(config ?? _validConfig, seed, name, NullLoggerFactory.Instance);
    }

    [Fact]
    public void Constructor_WithValidConfig_InitializesCorrectly()
    {
        // Arrange
        var generator = CreateGenerator(name: "Gen1");

        // Assert
        Assert.Equal("Gen1", generator.Name);
        Assert.False(generator.IsActive);
        Assert.Equal(0, generator.LoadsGeneratedCount);
        Assert.Null(generator.StartTime);
    }

    [Fact]
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

    [Fact]
    public void Initialize_NullScheduler_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = CreateGenerator();

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => generator.Initialize(null!));
        Assert.Equal("scheduler", ex.ParamName);
    }

    [Fact]
    public void Initialize_ValidScheduler_StoresScheduler()
    {
        // Arrange
        var generator = CreateGenerator();

        // Act
        generator.Initialize(_mockScheduler.Object);

        // Assert
        generator.ScheduleStartGenerating(_mockEngine.Object);
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<double>()), Times.AtLeastOnce);
    }

    [Fact]
    public void StartGenerating_BeforeInitialize_ThrowsInvalidOperationException()
    {
        // Arrange
        var generator = CreateGenerator();
        _currentTestTime = 0.0;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.ScheduleStartGenerating(_mockEngine.Object));
        Assert.Contains("has not been initialized", ex.Message);
    }

    [Fact]
    public void StartGenerating_NullEngine_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockScheduler.Object);
         
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => generator.ScheduleStartGenerating(null!));
        Assert.Equal("engine", ex.ParamName);
    }

    [Fact]
    public void StartGenerating_WhenInactive_SchedulesStartEvent()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockScheduler.Object);
        _currentTestTime = 10.0;

        // Act
        generator.ScheduleStartGenerating(_mockEngine.Object);

        // Assert
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<GeneratorStartEvent<DummyLoad>>(ev => ev.OwningGenerator == generator), // Check type and owner
            10.0), Times.Once);
    }

    private TEvent CaptureScheduledEvent<TEvent>(Action<IRunContext> actionToTriggerScheduling) where TEvent : AbstractEvent
    {
        AbstractEvent? capturedEvent = null;
        _mockScheduler.Setup(s => s.Schedule(It.IsAny<TEvent>(), It.IsAny<double>()))
                      .Callback<AbstractEvent, double>((ev, time) => capturedEvent = ev)
                      .Verifiable(); // Mark as verifiable

        actionToTriggerScheduling(_mockEngine.Object); // This will trigger the .Schedule call

        _mockScheduler.Verify(); // Verify the setup was called
        _mockScheduler.Reset();  // Reset Moq setup for next capture or verification

        Assert.NotNull(capturedEvent);
        Assert.IsType<TEvent>(capturedEvent);
        return (TEvent)capturedEvent;
    }


    [Fact]
    public void GeneratorStartEvent_Execute_WhenInactive_ActivatesGenerator_AndSchedulesArrive_NotSkippingFirst()
    {
        // Arrange
        var config = new GeneratorStaticConfig<DummyLoad>(_defaultInterArrivalTimeFunc, _defaultLoadFactoryFunc) { IsSkippingFirst = false };
        var generator = CreateGenerator(config: config);
        generator.Initialize(_mockScheduler.Object);
        _currentTestTime = 10.0;

        var startEvent = CaptureScheduledEvent<GeneratorStartEvent<DummyLoad>>(eng => generator.ScheduleStartGenerating(eng));

        // Act
        startEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.True(generator.IsActive);
        Assert.Equal(10.0, generator.StartTime);
        Assert.Equal(0, generator.LoadsGeneratedCount);
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<GeneratorArriveEvent<DummyLoad>>(ev => ev.OwningGenerator == generator),
            10.0), Times.Once); // Arrive scheduled for current time
    }

    [Fact]
    public void GeneratorStartEvent_Execute_WhenInactive_ActivatesGenerator_AndSchedulesArrive_SkippingFirst()
    {
        // Arrange
        var interArrivalTime = TimeSpan.FromSeconds(5);
        var config = new GeneratorStaticConfig<DummyLoad>((r) => interArrivalTime, _defaultLoadFactoryFunc) { IsSkippingFirst = true };
        var generator = CreateGenerator(config: config);
        generator.Initialize(_mockScheduler.Object);
        _currentTestTime = 10.0;

        var startEvent = CaptureScheduledEvent<GeneratorStartEvent<DummyLoad>>(eng => generator.ScheduleStartGenerating(eng));

        // Act
        startEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.True(generator.IsActive);
        Assert.Equal(10.0, generator.StartTime);
        Assert.Equal(0, generator.LoadsGeneratedCount);
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<GeneratorArriveEvent<DummyLoad>>(ev => ev.OwningGenerator == generator),
            10.0 + interArrivalTime.TotalSeconds), Times.Once); // Arrive scheduled after delay
    }

    [Fact]
    public void GeneratorStartEvent_Execute_WhenAlreadyActive_DoesNothing()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockScheduler.Object);
        _currentTestTime = 10.0;

        // First start and execution
        var firstStartEvent = CaptureScheduledEvent<GeneratorStartEvent<DummyLoad>>(eng => generator.ScheduleStartGenerating(eng));
        firstStartEvent.Execute(_mockEngine.Object);
        _mockScheduler.ResetCalls(); // Reset calls after first execution

        Assert.True(generator.IsActive);
        var initialStartTime = generator.StartTime;
        var initialLoadCount = generator.LoadsGeneratedCount;

        // Act: Schedule and execute another StartEvent
        _currentTestTime = 12.0; // Advance time slightly
        var secondStartEvent = CaptureScheduledEvent<GeneratorStartEvent<DummyLoad>>(eng => generator.ScheduleStartGenerating(eng));
        secondStartEvent.Execute(_mockEngine.Object);


        // Assert
        Assert.True(generator.IsActive); // Still active
        Assert.Equal(initialStartTime, generator.StartTime); // StartTime not changed
        Assert.Equal(initialLoadCount, generator.LoadsGeneratedCount); // Count not reset
        // Verify that ArriveEvent was not scheduled again by this second StartEvent execution
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<GeneratorArriveEvent<DummyLoad>>(), It.IsAny<double>()), Times.Never);
    }


    [Fact]
    public void StopGenerating_WhenActive_SchedulesStopEvent()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockScheduler.Object);
        // Make it active
        _currentTestTime = 10.0;
        var startEvent = CaptureScheduledEvent<GeneratorStartEvent<DummyLoad>>(eng => generator.ScheduleStartGenerating(eng));
        startEvent.Execute(_mockEngine.Object);
        Assert.True(generator.IsActive);
        _mockScheduler.ResetCalls();

        _currentTestTime = 20.0; // Advance time

        // Act
        generator.ScheduleStopGenerating(_mockEngine.Object);

        // Assert
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<GeneratorStopEvent<DummyLoad>>(ev => ev.OwningGenerator == generator),
            20.0), Times.Once);
    }

    [Fact]
    public void GeneratorStopEvent_Execute_WhenActive_DeactivatesGenerator()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockScheduler.Object);
        // Make it active
        _currentTestTime = 10.0;
        var startEvent = CaptureScheduledEvent<GeneratorStartEvent<DummyLoad>>(eng => generator.ScheduleStartGenerating(eng));
        startEvent.Execute(_mockEngine.Object);
        Assert.True(generator.IsActive);

        // Capture the StopEvent
        _currentTestTime = 20.0;
        var stopEvent = CaptureScheduledEvent<GeneratorStopEvent<DummyLoad>>(eng => generator.ScheduleStopGenerating(eng));

        // Act
        stopEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.False(generator.IsActive);
    }

    [Fact]
    public void HandleLoadGeneration_WhenActive_GeneratesLoad_RaisesEvent_And_SchedulesNextArrival()
    {
        // Arrange
        var interArrivalTime = TimeSpan.FromSeconds(7);
        int createdLoadValue = 42;
        var config = new GeneratorStaticConfig<DummyLoad>(
            (r) => interArrivalTime,
            (r) => new DummyLoad { Value = createdLoadValue }
        );
        // Note: We use a mock scheduler here
        var mockScheduler = new Mock<IScheduler>();
        var generator = new Generator<DummyLoad>(config, 123, "TestGen", new NullLoggerFactory());
        generator.Initialize(mockScheduler.Object);

        // Reset the mock to ignore the Schedule call from Initialize()
        mockScheduler.Reset();

        int actionCallCount = 0;
        DummyLoad? loadFromAction = null;
        double timeFromAction = 0.0;
        generator.LoadGenerated += (load, time) =>
        {
            actionCallCount++;
            loadFromAction = load;
            timeFromAction = time;
        };

        // Manually activate the generator for the test scenario
        // (This will schedule the first arrival, which we can ignore or verify if needed)
        generator.HandleActivation(30.0);
        mockScheduler.Reset(); // Reset again to ONLY focus on the HandleLoadGeneration call

        // Act
        generator.HandleLoadGeneration(30.0);

        // Assert
        Assert.Equal(1, generator.LoadsGeneratedCount);
        Assert.Equal(1, actionCallCount);
        Assert.NotNull(loadFromAction);
        Assert.Equal(createdLoadValue, loadFromAction!.Value);
        Assert.Equal(30.0, timeFromAction);

        // Verify that HandleLoadGeneration scheduled the NEXT arrival
        mockScheduler.Verify(s => s.Schedule(
            It.Is<GeneratorArriveEvent<DummyLoad>>(ev => ev.OwningGenerator == generator),
            30.0 + interArrivalTime.TotalSeconds),
            Times.Once);
    }

    [Fact]
    public void GeneratorArriveEvent_Execute_WhenInactive_DoesNothing()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockScheduler.Object);

        // Reset the mock to forget the call made by Initialize
        _mockScheduler.Invocations.Clear(); // Clear invocations but keep setups.

        generator.HandleDeactivation();
        _currentTestTime = 30.0;
        var arriveEvent = new GeneratorArriveEvent<DummyLoad>(generator);

        // Act
        arriveEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.Equal(0, generator.LoadsGeneratedCount);
        Assert.Equal(0, _loadFactoryCallCount);
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public void WarmedUp_ResetsStartTimeAndLoadCount()
    {
        // Arrange
        var generator = CreateGenerator();
        generator.Initialize(_mockScheduler.Object);
        generator.HandleActivation(10.0);

        // Act
        generator.WarmedUp(simulationTime: 100.0);

        // Assert
        Assert.Equal(100.0, generator.StartTime);
        Assert.Equal(0, generator.LoadsGeneratedCount);
        Assert.True(generator.IsActive); // WarmedUp should not change IsActive state
    }
}