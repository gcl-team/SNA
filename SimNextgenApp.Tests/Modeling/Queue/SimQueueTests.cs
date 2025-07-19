using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Statistics;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Modeling.Queue;

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
        var queue = new SimQueue<DummyLoad>(config ?? _defaultInfiniteConfig, name, _nullLoggerFactory);

        queue.Initialize(_mockEngineContext.Object);

        return queue;
    }

    [Fact(DisplayName = "Constructor with infinite capacity should initialise properties correctly.")]
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

    [Fact(DisplayName = "Constructor with finite capacity should initialise properties correctly.")]
    public void Constructor_WithValidFiniteConfig_InitializesCorrectly()
    {
        // Arrange
        var queue = CreateQueue(config: _defaultFiniteConfig, name: "FiniteQ_Cap2");

        // Assert
        Assert.Equal("FiniteQ_Cap2", queue.Name);
        Assert.Equal(0, queue.Occupancy);
        Assert.Equal(2, queue.Vacancy);
        Assert.Equal(2, queue.Capacity);
        Assert.True(queue.ToDequeue);
        Assert.Empty(queue.WaitingItems);

        // --- Statistics ---
        Assert.NotNull(queue.TimeBasedMetric);
        Assert.Equal(0, queue.TimeBasedMetric.CurrentCount);
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException if config is null.")]
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

    [Fact(DisplayName = "Constructor should throw ArgumentNullException if logger factory is null.")]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SimQueue<DummyLoad>(_defaultInfiniteConfig, DefaultQueueName, null!)
        );
        Assert.Equal("loggerFactory", ex.ParamName);
    }

    [Theory(DisplayName = "Constructor should throw for invalid capacity values.")]
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
        Assert.Equal("config", ex.ParamName);
    }

    [Fact(DisplayName = "Initialize should prepare the TimeBasedMetric correctly.")]
    public void Initialize_WithValidContext_InitializesMetric()
    {
        // Arrange
        var queue = CreateQueue();
        _currentTestTime = 0.0;

        // Act
        queue.Initialize(_mockEngineContext.Object);

        // Assert
        Assert.Equal(0, queue.TimeBasedMetric.CurrentCount);
        Assert.Equal(0.0, queue.TimeBasedMetric.CurrentTime);
        Assert.Equal(0.0, queue.TimeBasedMetric.InitialTime);
    }

    [Fact(DisplayName = "Initialize should throw ArgumentNullException for a null context.")]
    public void Initialize_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => queue.Initialize(null!));
    }

    [Fact(DisplayName = "TryScheduleEnqueue should schedule an EnqueueEvent when vacancy exists.")]
    public void TryScheduleEnqueue_WithVacancy_SchedulesEventAndReturnsTrue()
    {
        // Arrange
        var queue = CreateQueue(_defaultFiniteConfig);
        queue.Initialize(_mockEngineContext.Object);
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

    [Fact(DisplayName = "TryScheduleEnqueue for an infinite queue should always succeed.")]
    public void TryScheduleEnqueue_InfiniteQueue_AlwaysSchedulesAndReturnsTrue()
    {
        // Arrange
        var queue = CreateQueue(_defaultInfiniteConfig);
        queue.Initialize(_mockEngineContext.Object);
        _currentTestTime = 5.0;
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

    [Fact(DisplayName = "TryScheduleEnqueue for a full queue should balk and fire LoadBalked event.")]
    public void TryScheduleEnqueue_FullQueue_BalksAndFiresEvent()
    {
        // Arrange
        var queue = CreateQueue(_defaultFiniteConfig);
        queue.Initialize(_mockEngineContext.Object);
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

    [Fact(DisplayName = "TryScheduleEnqueue should throw for a null load.")]
    public void TryScheduleEnqueue_NullLoad_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockEngineContext.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>("load", () => queue.TryScheduleEnqueue(null!, _mockEngineContext.Object));
    }

    [Fact(DisplayName = "TryScheduleEnqueue should throw for a null context.")]
    public void TryScheduleEnqueue_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockEngineContext.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>("engineContext", () => queue.TryScheduleEnqueue(new DummyLoad(), null!));
    }

    [Fact(DisplayName = "ScheduleUpdateToDequeue should schedule an UpdateToDequeueEvent.")]
    public void ScheduleUpdateToDequeue_SchedulesUpdateEvent()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockEngineContext.Object);
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

    [Fact(DisplayName = "ScheduleUpdateToDequeue should throw for a null context.")]
    public void ScheduleUpdateToDequeue_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockEngineContext.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>("engineContext", () => queue.ScheduleUpdateToDequeue(false, null!));
    }

    // We test the internal 'Handle' methods directly to verify the core logic of the queue
    // without needing to instantiate and execute events in a full simulation run.

    [Fact(DisplayName = "HandleEnqueue should add a load and update metrics.")]
    public void HandleEnqueue_UpdatesStateAndMetrics()
    {
        // Arrange
        var queue = CreateQueue();
        var load = new DummyLoad();

        // Act
        queue.HandleEnqueue(load, 10.0);

        // Assert
        Assert.Equal(1, queue.Occupancy);
        Assert.Contains(load, queue.WaitingItems);
        Assert.Equal(1, queue.TimeBasedMetric.CurrentCount);
    }

    [Fact(DisplayName ="HandleDequeue should remove a load and update metrics when enabled.")]
    public void HandleDequeue_WhenEnabled_RemovesLoadAndUpdatesMetrics()
    {
        // Arrange
        var queue = CreateQueue();
        var load1 = new DummyLoad("L1");
        var load2 = new DummyLoad("L2");
        queue.HandleEnqueue(load1, 5.0);
        queue.HandleEnqueue(load2, 6.0);

        // Act
        queue.HandleDequeue(10.0);

        // Assert
        Assert.Equal(1, queue.Occupancy);
        Assert.Equal(1, queue.TimeBasedMetric.CurrentCount);
        var remainingLoad = Assert.Single(queue.WaitingItems);
        Assert.Same(load2, remainingLoad); // Verifies FIFO
        Assert.Equal(1, queue.TimeBasedMetric.CurrentCount);
    }

    [Fact(DisplayName ="HandleUpdateToDequeue should update the ToDequeue property.")]
    public void HandleUpdateToDequeue_UpdatesProperty()
    {
        // Arrange
        var queue = CreateQueue();
        Assert.True(queue.ToDequeue); // Default is true

        // Act
        queue.HandleUpdateToDequeue(false, 10.0);

        // Assert
        Assert.False(queue.ToDequeue);
    }

    [Fact(DisplayName = "HandleDequeue should do nothing if ToDequeue is false.")]
    public void HandleDequeue_WhenDisabled_DoesNothing()
    {
        // Arrange
        var queue = CreateQueue();
        queue.HandleEnqueue(new DummyLoad(), 5.0);
        queue.HandleUpdateToDequeue(false, 6.0); // Disable dequeuing

        // Act
        queue.HandleDequeue(10.0);

        // Assert
        Assert.Equal(1, queue.Occupancy); // State is unchanged
    }

    [Fact(DisplayName = "ToDequeue property should default to true.")]
    public void ToDequeue_DefaultIsTrue()
    {
        // Arrange
        var queue = CreateQueue();

        // Act & Assert
        Assert.True(queue.ToDequeue);
    }

    [Fact(DisplayName = "WarmedUp should reset the metric with the current occupancy.")]
    public void WarmedUp_ResetsMetricAndObservesCurrentOccupancy()
    {
        // Arrange
        var queue = CreateQueue();
        queue.Initialize(_mockEngineContext.Object);

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

}
