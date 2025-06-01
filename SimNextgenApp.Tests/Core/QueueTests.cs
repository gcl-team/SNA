using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;
using SimCore = SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Core;

public class QueueTests
{
    private const string DefaultQueueName = "TestQueue";
    private readonly Mock<IScheduler> _mockScheduler;
    private readonly Mock<IRunContext> _mockEngineContext; // Renamed for clarity vs. SimulationEngine
    private readonly ILoggerFactory _nullLoggerFactory;

    private QueueStaticConfig<DummyLoad> _defaultInfiniteConfig;
    private QueueStaticConfig<DummyLoad> _defaultFiniteConfig;
    private double _currentTestTime = 0.0;

    public QueueTests()
    {
        _nullLoggerFactory = NullLoggerFactory.Instance;
        _mockScheduler = new Mock<IScheduler>();

        _mockEngineContext = new Mock<IRunContext>();
        _mockEngineContext.SetupGet(e => e.ClockTime).Returns(() => _currentTestTime);
        _mockEngineContext.SetupGet(e => e.Scheduler).Returns(_mockScheduler.Object);
        
        _defaultInfiniteConfig = new QueueStaticConfig<DummyLoad>();
        _defaultFiniteConfig = new QueueStaticConfig<DummyLoad> { Capacity = 2 };
    }

    private SimCore.Queue<DummyLoad> CreateQueue(
        QueueStaticConfig<DummyLoad>? config = null,
        string name = DefaultQueueName)
    {
        return new SimCore.Queue<DummyLoad>(config ?? _defaultInfiniteConfig, name, _nullLoggerFactory);
    }

    // --- Constructor Tests ---
    [Fact]
    public void Constructor_WithValidInfiniteConfig_InitializesCorrectly()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultInfiniteConfig, name: "InfiniteQ");

        // Assert
        Assert.Equal("InfiniteQ", queue.Name);
        Assert.Equal(0, queue.Occupancy);
        Assert.Equal(int.MaxValue, queue.Vacancy); // From config
        Assert.True(queue.ToDequeue);
        Assert.NotNull(queue.TimeBasedMetric);
        Assert.Equal(0, queue.TimeBasedMetric.CurrentCount);
        Assert.NotNull(queue.OnEnqueueActions);
        Assert.Empty(queue.OnEnqueueActions);
        Assert.NotNull(queue.OnDequeueActions);
        Assert.Empty(queue.OnDequeueActions);
        Assert.NotNull(queue.OnBalkActions);
        Assert.Empty(queue.OnBalkActions);
        Assert.NotNull(queue.OnStateChangeActions);
        Assert.Empty(queue.OnStateChangeActions);
    }

    [Fact]
    public void Constructor_WithValidFiniteConfig_InitializesCorrectly()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultFiniteConfig, name: "FiniteQ_Cap2");

        // Assert
        Assert.Equal("FiniteQ_Cap2", queue.Name);
        Assert.Equal(0, queue.Occupancy);
        Assert.Equal(2, queue.Vacancy); // From config
        Assert.True(queue.ToDequeue);
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        QueueStaticConfig<DummyLoad> nullConfig = null!;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SimCore.Queue<DummyLoad>(nullConfig, DefaultQueueName, _nullLoggerFactory)
        );
        Assert.Equal("config", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SimCore.Queue<DummyLoad>(_defaultInfiniteConfig, DefaultQueueName, null!)
        );
        Assert.Equal("loggerFactory", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidCapacityInConfig_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        // Arrange
        var invalidConfig = new QueueStaticConfig<DummyLoad> { Capacity = invalidCapacity };

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SimCore.Queue<DummyLoad>(invalidConfig, DefaultQueueName, _nullLoggerFactory)
        );
        Assert.Equal("config", ex.ParamName); // The constructor checks config.Capacity
    }

    // --- Initialize Method Tests ---
    [Fact]
    public void Initialize_WithValidScheduler_SetsSchedulerAndInitializesMetric()
    {
        // Arrange
        var queue = CreateQueue();
        _currentTestTime = 0.0; // Ensure metric observes at 0

        // Act
        queue.Initialize(_mockScheduler.Object);

        // Assert
        // Check internal _scheduler (hard to assert directly without exposing it,
        // but EnsureSchedulerInitialized in other methods will test it indirectly)
        // We can verify TimeBasedMetric was initialized correctly.
        Assert.Equal(0, queue.TimeBasedMetric.CurrentCount);
        Assert.Equal(0.0, queue.TimeBasedMetric.CurrentTime);
        Assert.Equal(0.0, queue.TimeBasedMetric.InitialTime);
    }

    [Fact]
    public void Initialize_NullScheduler_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => queue.Initialize(null!));
    }

    // --- WarmedUp Method Tests ---
    [Fact]
    public void WarmedUp_ResetsMetricAndObservesCurrentOccupancy()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockScheduler.Object);
        // Simulate some activity before warmup
        _currentTestTime = 10.0;
        queue.Waiting.Add(new DummyLoad()); // Manually add to simulate pre-warmup state
        queue.Waiting.Add(new DummyLoad());
        queue.TimeBasedMetric.ObserveCount(2, _currentTestTime); // Simulate metric update

        double warmupTime = 20.0;
        _currentTestTime = warmupTime;

        // Act
        queue.WarmedUp(warmupTime);

        // Assert
        Assert.Equal(warmupTime, queue.TimeBasedMetric.InitialTime);
        Assert.Equal(warmupTime, queue.TimeBasedMetric.CurrentTime);
        Assert.Equal(2, queue.TimeBasedMetric.CurrentCount); // Should observe the 2 items
        Assert.Equal(2, queue.Occupancy); // Occupancy should remain
    }

    // --- TryScheduleEnqueue Method Tests ---
    [Fact]
    public void TryScheduleEnqueue_WhenQueueHasVacancy_SchedulesEnqueueEventAndReturnsTrue()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultFiniteConfig); // Capacity 2
        queue.Initialize(_mockScheduler.Object);
        var load = new DummyLoad();
        _currentTestTime = 5.0;

        // Act
        bool result = queue.TryScheduleEnqueue(load, _mockEngineContext.Object);

        // Assert
        Assert.True(result);
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<EnqueueEvent<DummyLoad>>(e => e.LoadToEnqueue == load && e.OwningQueue == queue),
            _currentTestTime),
            Times.Once);
    }

    [Fact]
    public void TryScheduleEnqueue_WhenInfiniteQueue_AlwaysSchedulesAndReturnsTrue()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultInfiniteConfig);
        queue.Initialize(_mockScheduler.Object);
        queue.Waiting.Add(new DummyLoad()); // Add some items
        queue.Waiting.Add(new DummyLoad());
        var load = new DummyLoad();
        _currentTestTime = 5.0;

        // Act
        bool result = queue.TryScheduleEnqueue(load, _mockEngineContext.Object);

        // Assert
        Assert.True(result);
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<EnqueueEvent<DummyLoad>>(e => e.LoadToEnqueue == load && e.OwningQueue == queue),
            _currentTestTime),
            Times.Once);
    }

    [Fact]
    public void TryScheduleEnqueue_WhenFiniteQueueIsFull_BalksAndReturnsFalse_DoesNotSchedule()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultFiniteConfig); // Capacity 2
        queue.Initialize(_mockScheduler.Object);
        queue.Waiting.Add(new DummyLoad("L1")); // Fill the queue
        queue.Waiting.Add(new DummyLoad("L2"));
        Assert.Equal(0, queue.Vacancy); // Verify it's full

        var loadToBalk = new DummyLoad("L3");
        _currentTestTime = 5.0;
        bool balkActionCalled = false;
        queue.OnBalkActions.Add((balkedLoad, time) => {
            Assert.Same(loadToBalk, balkedLoad);
            Assert.Equal(_currentTestTime, time);
            balkActionCalled = true;
        });


        // Act
        bool result = queue.TryScheduleEnqueue(loadToBalk, _mockEngineContext.Object);

        // Assert
        Assert.False(result);
        Assert.True(balkActionCalled, "OnBalkAction was not called.");
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<EnqueueEvent<DummyLoad>>(), It.IsAny<double>()), Times.Never);
    }

    [Fact]
    public void TryScheduleEnqueue_NullLoad_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockScheduler.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>("load", () => queue.TryScheduleEnqueue(null!, _mockEngineContext.Object));
    }

    [Fact]
    public void TryScheduleEnqueue_NullEngineContext_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockScheduler.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>("engineContext", () => queue.TryScheduleEnqueue(new DummyLoad(), null!));
    }

    [Fact]
    public void TryScheduleEnqueue_BeforeInitialize_ThrowsInvalidOperationException()
    {
        // Arrange
        var queue = CreateQueue(); // Not initialized

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => queue.TryScheduleEnqueue(new DummyLoad(), _mockEngineContext.Object));
    }


    // --- ScheduleUpdateToDequeue Method Tests ---
    [Fact]
    public void ScheduleUpdateToDequeue_SchedulesUpdateEvent()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockScheduler.Object);
        _currentTestTime = 10.0;
        bool newDequeueState = false;

        // Act
        queue.ScheduleUpdateToDequeue(newDequeueState, _mockEngineContext.Object);

        // Assert
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<UpdateToDequeueEvent<DummyLoad>>(e => e.NewToDequeueState == newDequeueState && e.OwningQueue == queue),
            _currentTestTime),
            Times.Once);
    }

    [Fact]
    public void ScheduleUpdateToDequeue_NullEngineContext_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockScheduler.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>("engineContext", () => queue.ScheduleUpdateToDequeue(false, null!));
    }

    [Fact]
    public void ScheduleUpdateToDequeue_BeforeInitialize_ThrowsInvalidOperationException()
    {
        // Arrange
        var queue = CreateQueue(); // Not initialized

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => queue.ScheduleUpdateToDequeue(false, _mockEngineContext.Object));
    }

    // --- Property Tests (brief examples, Occupancy/Vacancy covered by constructor/enqueue tests) ---
    [Fact]
    public void ToDequeue_DefaultIsTrue()
    {
        // Arrange
        var queue = CreateQueue();
        // Act & Assert
        Assert.True(queue.ToDequeue);
    }
}
