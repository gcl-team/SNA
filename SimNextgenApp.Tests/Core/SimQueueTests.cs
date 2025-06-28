using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;

namespace SimNextgenApp.Tests.Core;

public class SimQueueTests
{
    private const string DefaultQueueName = "TestQueue";
    private readonly Mock<IScheduler> _mockScheduler;
    private readonly Mock<IRunContext> _mockEngineContext;
    private readonly ILoggerFactory _nullLoggerFactory;
    private readonly QueueStaticConfig<DummyLoad> _defaultInfiniteConfig;
    private readonly QueueStaticConfig<DummyLoad> _defaultFiniteConfig;

    private double _currentTestTime = 0.0;

    public SimQueueTests()
    {
        _nullLoggerFactory = NullLoggerFactory.Instance;
        _mockScheduler = new Mock<IScheduler>();

        _mockEngineContext = new Mock<IRunContext>();
        _mockEngineContext.SetupGet(e => e.ClockTime).Returns(() => _currentTestTime);
        _mockEngineContext.SetupGet(e => e.Scheduler).Returns(_mockScheduler.Object);
        
        _defaultInfiniteConfig = new QueueStaticConfig<DummyLoad>();
        _defaultFiniteConfig = new QueueStaticConfig<DummyLoad> { Capacity = 2 };
    }

    private SimQueue<DummyLoad> CreateQueue(
        QueueStaticConfig<DummyLoad>? config = null,
        string name = DefaultQueueName)
    {
        return new SimQueue<DummyLoad>(config ?? _defaultInfiniteConfig, name, _nullLoggerFactory);
    }

    [Fact]
    public void Constructor_WithValidInfiniteConfig_InitializesCorrectly()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultInfiniteConfig, name: "InfiniteQ");

        // Assert
        // --- Core Properties ---
        Assert.Equal("InfiniteQ", queue.Name);
        Assert.Equal(0, queue.Occupancy);
        Assert.Equal(int.MaxValue, queue.Vacancy);
        Assert.Equal(int.MaxValue, queue.Capacity);
        Assert.True(queue.ToDequeue);
        Assert.Empty(queue.WaitingItems);

        // --- Statistics ---
        Assert.NotNull(queue.TimeBasedMetric);
        Assert.Equal(0, queue.TimeBasedMetric.CurrentCount);
    }

    [Fact]
    public void Constructor_WithValidFiniteConfig_InitializesCorrectly()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultFiniteConfig, name: "FiniteQ_Cap2");

        // Assert
        Assert.Equal("FiniteQ_Cap2", queue.Name);
        Assert.Equal(0, queue.Occupancy);
        Assert.Equal(2, queue.Vacancy);
        Assert.True(queue.ToDequeue);
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        QueueStaticConfig<DummyLoad> nullConfig = null!;

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SimQueue<DummyLoad>(nullConfig, DefaultQueueName, _nullLoggerFactory)
        );
        Assert.Equal("config", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SimQueue<DummyLoad>(_defaultInfiniteConfig, DefaultQueueName, null!)
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
            new SimQueue<DummyLoad>(invalidConfig, DefaultQueueName, _nullLoggerFactory)
        );
        Assert.Equal("config", ex.ParamName); // The constructor checks config.Capacity
    }

    [Fact]
    public void Initialize_WithValidScheduler_SetsSchedulerAndInitializesMetric()
    {
        // Arrange
        var queue = CreateQueue();
        _currentTestTime = 0.0;

        // Act
        queue.Initialize(_mockScheduler.Object);

        // Assert
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

    [Fact]
    public void WarmedUp_ResetsMetricAndObservesCurrentOccupancy()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockScheduler.Object);

        // Simulate some activity before warmup
        _currentTestTime = 5.0;
        queue.HandleEnqueue(new DummyLoad("L1"), _currentTestTime);
        _currentTestTime = 10.0;
        queue.HandleEnqueue(new DummyLoad("L2"), _currentTestTime);

        // Sanity check the pre-warmup state
        Assert.Equal(2, queue.Occupancy);

        double warmupTime = 20.0;
        _currentTestTime = warmupTime;

        // Act
        queue.WarmedUp(warmupTime);

        // Assert
        Assert.Equal(warmupTime, queue.TimeBasedMetric.InitialTime);
        Assert.Equal(warmupTime, queue.TimeBasedMetric.CurrentTime);
        Assert.Equal(2, queue.TimeBasedMetric.CurrentCount); // Should observe the 2 items
        Assert.Equal(2, queue.Occupancy); // Occupancy should remain
        Assert.Equal(2, queue.WaitingItems.Count());
    }

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
        _currentTestTime = 5.0;

        queue.HandleEnqueue(new DummyLoad("L1"), 1.0);
        queue.HandleEnqueue(new DummyLoad("L2"), 2.0);

        var loadToEnqueue = new DummyLoad("L3");

        // Act
        bool result = queue.TryScheduleEnqueue(loadToEnqueue, _mockEngineContext.Object);

        // Assert
        // The method should report success.
        Assert.True(result);

        // It should have scheduled exactly one EnqueueEvent with the correct details.
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<EnqueueEvent<DummyLoad>>(e => e.LoadToEnqueue == loadToEnqueue && e.OwningQueue == queue),
            _currentTestTime),
            Times.Once);
    }

    [Fact]
    public void TryScheduleEnqueue_WhenFiniteQueueIsFull_BalksAndReturnsFalse_AndFiresEvent()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultFiniteConfig);
        queue.Initialize(_mockScheduler.Object);
        _currentTestTime = 5.0;

        // 1. Arrange State: Fill the queue to capacity using its internal handler.
        queue.HandleEnqueue(new DummyLoad("L1"), 1.0);
        queue.HandleEnqueue(new DummyLoad("L2"), 2.0);
        Assert.Equal(0, queue.Vacancy); // Verify our setup is correct: the queue is full.

        var loadToBalk = new DummyLoad("L3");

        // 2. Subscribe to Event: Set up a handler to listen for the balk event.
        bool eventFired = false;
        queue.LoadBalked += (balkedLoad, balkTime) =>
        {
            eventFired = true;
            Assert.Same(loadToBalk, balkedLoad); // Check if the correct load was reported
            Assert.Equal(_currentTestTime, balkTime); // Check if it happened at the correct time
        };

        // Act
        bool result = queue.TryScheduleEnqueue(loadToBalk, _mockEngineContext.Object);

        // Assert
        // 1. The method should report failure (balking).
        Assert.False(result);

        // 2. The LoadBalked event must have been fired.
        Assert.True(eventFired, "The LoadBalked event was not fired.");

        // 3. No EnqueueEvent should have been scheduled with the scheduler.
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

    [Fact]
    public void ToDequeue_DefaultIsTrue()
    {
        // Arrange
        var queue = CreateQueue();
        // Act & Assert
        Assert.True(queue.ToDequeue);
    }
}
