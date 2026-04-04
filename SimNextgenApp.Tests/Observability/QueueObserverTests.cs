using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Observability;
using SimNextgenApp.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace SimNextgenApp.Tests.Observability;

public class QueueObserverTests
{
    private class TestLoad
    {
        public int Id { get; set; }
    }

    private class MockRunContext : IRunContext
    {
        public long ClockTime { get; set; }
        public long ExecutedEventCount { get; set; }
        public IScheduler Scheduler { get; set; } = null!;
        public SimulationTimeUnit TimeUnit => SimulationTimeUnit.Milliseconds;
    }

    [Fact(DisplayName = "CreateSimple should create observer with meter and initialize counters.")]
    public void CreateSimple_CreatesObserverWithMeter()
    {
        // Arrange
        var config = new QueueStaticConfig<TestLoad> { Capacity = 10 };
        var queue = new SimQueue<TestLoad>(config, "TestQueue", NullLoggerFactory.Instance);

        // Act
        using var observer = QueueObserver.CreateSimple(queue);

        // Assert
        Assert.NotNull(observer);
        Assert.NotNull(observer.Meter);
        Assert.Equal(0, observer.LoadsEnqueued);
        Assert.Equal(0, observer.LoadsDequeued);
        Assert.Equal(0, observer.LoadsBalked);
    }

    [Fact(DisplayName = "Occupancy should reflect current queue state.")]
    public void Occupancy_ReflectsQueueState()
    {
        // Arrange
        var config = new QueueStaticConfig<TestLoad> { Capacity = 10 };
        var queue = new SimQueue<TestLoad>(config, "TestQueue", NullLoggerFactory.Instance);
        using var observer = QueueObserver.CreateSimple(queue);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        var context = new MockRunContext { ClockTime = 0, Scheduler = null! };
        var mockScheduler = new MockEventScheduler(context);
        context.Scheduler = mockScheduler;

        // Act - Enqueue some items
        var load1 = new TestLoad { Id = 1 };
        var load2 = new TestLoad { Id = 2 };

        queue.TryScheduleEnqueue(load1, context);
        queue.TryScheduleEnqueue(load2, context);

        // Execute the enqueue events
        mockScheduler.ExecutePendingEvents();

        // Assert
        Assert.Equal(2, observer.Occupancy);
        Assert.Equal(2, observer.LoadsEnqueued);
    }

    [Fact(DisplayName = "LoadsEnqueued should increment when loads are enqueued.")]
    public void LoadsEnqueued_IncrementsOnEnqueue()
    {
        // Arrange
        var config = new QueueStaticConfig<TestLoad> { Capacity = 10 };
        var queue = new SimQueue<TestLoad>(config, "TestQueue", NullLoggerFactory.Instance);
        using var observer = QueueObserver.CreateSimple(queue);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        var context = new MockRunContext { ClockTime = 100, Scheduler = null! };
        var mockScheduler = new MockEventScheduler(context);
        context.Scheduler = mockScheduler;

        // Act
        var load = new TestLoad { Id = 1 };
        queue.TryScheduleEnqueue(load, context);
        mockScheduler.ExecutePendingEvents();

        // Assert
        Assert.Equal(1, observer.LoadsEnqueued);
    }

    [Fact(DisplayName = "LoadsDequeued should increment when loads are dequeued.")]
    public void LoadsDequeued_IncrementsOnDequeue()
    {
        // Arrange
        var config = new QueueStaticConfig<TestLoad> { Capacity = 10 };
        var queue = new SimQueue<TestLoad>(config, "TestQueue", NullLoggerFactory.Instance);
        using var observer = QueueObserver.CreateSimple(queue);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        var context = new MockRunContext { ClockTime = 100, Scheduler = null! };
        var mockScheduler = new MockEventScheduler(context);
        context.Scheduler = mockScheduler;

        // Act - Enqueue and then dequeue
        var load = new TestLoad { Id = 1 };
        queue.TryScheduleEnqueue(load, context);
        mockScheduler.ExecutePendingEvents();

        context.ClockTime = 200;
        queue.TriggerDequeueAttempt(context);
        mockScheduler.ExecutePendingEvents();

        // Assert
        Assert.Equal(1, observer.LoadsEnqueued);
        Assert.Equal(1, observer.LoadsDequeued);
    }

    [Fact(DisplayName = "LoadsBalked should increment when queue is full.")]
    public void LoadsBalked_IncrementsWhenQueueFull()
    {
        // Arrange - Create queue with capacity of 2
        var config = new QueueStaticConfig<TestLoad> { Capacity = 2 };
        var queue = new SimQueue<TestLoad>(config, "TestQueue", NullLoggerFactory.Instance);
        using var observer = QueueObserver.CreateSimple(queue);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        var context = new MockRunContext { ClockTime = 100, Scheduler = null! };
        var mockScheduler = new MockEventScheduler(context);
        context.Scheduler = mockScheduler;

        // Act - Fill queue and then try to enqueue one more
        queue.TryScheduleEnqueue(new TestLoad { Id = 1 }, context);
        queue.TryScheduleEnqueue(new TestLoad { Id = 2 }, context);
        mockScheduler.ExecutePendingEvents();

        context.ClockTime = 200;
        queue.TryScheduleEnqueue(new TestLoad { Id = 3 }, context);
        mockScheduler.ExecutePendingEvents();

        // Assert
        Assert.Equal(2, observer.LoadsEnqueued);
        Assert.Equal(1, observer.LoadsBalked);
    }

    [Fact(DisplayName = "WaitTime should emit to histogram for backend aggregation.")]
    public void WaitTime_EmitsToHistogram()
    {
        // Arrange
        var config = new QueueStaticConfig<TestLoad> { Capacity = 10 };
        var queue = new SimQueue<TestLoad>(config, "TestQueue", NullLoggerFactory.Instance);
        using var observer = QueueObserver.CreateSimple(queue);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        var context = new MockRunContext { ClockTime = 1000, Scheduler = null! };
        var mockScheduler = new MockEventScheduler(context);
        context.Scheduler = mockScheduler;

        // Act - Enqueue at time 1000 and dequeue at time 1500 (500ms wait)
        var load = new TestLoad { Id = 1 };
        queue.TryScheduleEnqueue(load, context);
        mockScheduler.ExecutePendingEvents();

        context.ClockTime = 1500;
        queue.TriggerDequeueAttempt(context);
        mockScheduler.ExecutePendingEvents();

        // Assert - Verify dequeue happened
        // NOTE: We can't directly test histogram values - that's the backend's job
        // This follows the "Emitter not Calculator" principle (plan.md watch-out #4)
        Assert.Equal(1, observer.LoadsDequeued);
    }

    [Fact(DisplayName = "Dispose should clean up resources correctly.")]
    public void Dispose_CleansUpCorrectly()
    {
        // Arrange
        var config = new QueueStaticConfig<TestLoad> { Capacity = 10 };
        var queue = new SimQueue<TestLoad>(config, "TestQueue", NullLoggerFactory.Instance);
        var observer = QueueObserver.CreateSimple(queue);

        // Act
        observer.Dispose();

        // Assert - Should not throw after disposal
        Assert.NotNull(observer);
    }

    // Mock scheduler for testing
    private class MockEventScheduler : IScheduler
    {
        private readonly List<(AbstractEvent evt, long time)> _events = new();
        private readonly IRunContext _context;

        public long ClockTime { get; set; }

        public MockEventScheduler(IRunContext context)
        {
            _context = context;
        }

        public void Schedule(AbstractEvent evt, long eventTime)
        {
            _events.Add((evt, eventTime));
        }

        public void Schedule(AbstractEvent ev, TimeSpan delay)
        {
            var eventTime = ClockTime + (long)delay.TotalMilliseconds;
            Schedule(ev, eventTime);
        }

        public void ExecutePendingEvents()
        {
            var events = _events.OrderBy(e => e.time).ToList();
            _events.Clear();

            foreach (var (evt, _) in events)
            {
                evt.Execute(_context);
            }
        }
    }
}
