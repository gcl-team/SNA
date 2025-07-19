using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Events;
using SimNextgenApp.Exceptions;
using SimNextgenApp.Modeling;
using SimNextgenApp.Statistics;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Core;

public class SimulationEngineTests
{
    private readonly Mock<ISimulationModel> _mockModel;
    private readonly Mock<IRunStrategy> _mockStrategy;
    private readonly Mock<ISimulationTracer> _mockTracer;

    public SimulationEngineTests()
    {
        _mockModel = new Mock<ISimulationModel>();
        _mockStrategy = new Mock<IRunStrategy>();
        _mockTracer = new Mock<ISimulationTracer>();
    }

    private SimulationEngine CreateEngine(SimulationProfile profile) => new(profile);

    private SimulationProfile CreateProfile() => new(
        _mockModel.Object,
        _mockStrategy.Object,
        "TestProfile",
        SimulationTimeUnit.Seconds,
        NullLoggerFactory.Instance,
        _mockTracer.Object
    );

    [Fact(DisplayName = "Run should initialize the model and execute a scheduled event.")]
    public void Run_InitializesModelAndExecutesEvents()
    {
        // Arrange
        var testEvent = new TestEvent();
        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
                  .Callback<IRunContext>(ctx => ctx.Scheduler.Schedule(testEvent, 1.0));

        // Let the loop run once for the event, then stop.
        int calls = 0;
        _mockStrategy.Setup(s => s.ShouldContinue(It.IsAny<IRunContext>()))
                     .Returns(() => ++calls <= 1);

        var engine = CreateEngine(CreateProfile());

        // Act
        var result = engine.Run();

        // Assert
        Assert.True(testEvent.Executed);
        Assert.Equal(1.0, engine.ClockTime);
        Assert.Equal(1, result.ExecutedEventCount);
        _mockModel.Verify(m => m.Initialize(It.IsAny<IRunContext>()), Times.Once);
    }

    [Fact(DisplayName = "Run should execute events in strict chronological order.")]
    public void Run_ExecutesEventsInCorrectOrder()
    {
        // Arrange
        var executedEvents = new List<string>();
        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
            .Callback<IRunContext>(ctx =>
            {
                ctx.Scheduler.Schedule(new NamedEvent("C", executedEvents), 3.0);
                ctx.Scheduler.Schedule(new NamedEvent("A", executedEvents), 1.0);
                ctx.Scheduler.Schedule(new NamedEvent("B", executedEvents), 2.0);
            });

        // Stop after 3 events
        _mockStrategy.Setup(s => s.ShouldContinue(It.Is<IRunContext>(ctx => ctx.ExecutedEventCount < 3)))
                     .Returns(true);

        var engine = CreateEngine(CreateProfile());

        // Act
        engine.Run();

        // Assert
        Assert.Equal(new[] { "A", "B", "C" }, executedEvents);
    }

    [Fact(DisplayName = "Run should break ties for same-time events using scheduling order (FIFO).")]
    public void Run_SameTimeEvents_ExecutesInSchedulingOrder()
    {
        // Arrange
        var executedEvents = new List<string>();
        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
            .Callback<IRunContext>(ctx =>
            {
                ctx.Scheduler.Schedule(new NamedEvent("First", executedEvents), 2.0);
                ctx.Scheduler.Schedule(new NamedEvent("Second", executedEvents), 2.0);
            });

        _mockStrategy.Setup(s => s.ShouldContinue(It.Is<IRunContext>(ctx => ctx.ExecutedEventCount < 2)))
                     .Returns(true);

        var engine = CreateEngine(CreateProfile());

        // Act
        engine.Run();

        // Assert
        Assert.Equal(new[] { "First", "Second" }, executedEvents);
    }

    [Fact(DisplayName ="Run should throw InvalidOperationException if called a second time.")]
    public void Run_CalledTwice_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockStrategy.Setup(s => s.ShouldContinue(It.IsAny<IRunContext>())).Returns(false);
        var engine = CreateEngine(CreateProfile());
        engine.Run(); // First successful run

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }

    [Fact(DisplayName = "Run should call WarmedUp on IWarmupAware models when warmup time is reached.")]
    public void Run_WarmupTimeReached_CallsWarmedUp()
    {
        // Arrange
        var warmupModelMock = _mockModel.As<IWarmupAware>();
        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
                  .Callback<IRunContext>(ctx => ctx.Scheduler.Schedule(new TestEvent(), 5.0));

        _mockStrategy.SetupGet(s => s.WarmupEndTime).Returns(5.0);
        _mockStrategy.Setup(s => s.ShouldContinue(It.IsAny<IRunContext>())).Returns(true);

        var engine = CreateEngine(CreateProfile());

        // Act
        engine.Run();

        // Assert
        warmupModelMock.Verify(m => m.WarmedUp(5.0), Times.Once);
    }

    [Fact(DisplayName = "Run should throw SimulationException if Model.Initialize fails.")]
    public void Run_ModelInitializationFails_ThrowsSimulationException()
    {
        // Arrange
        var initException = new InvalidProgramException("Model failed to init!");
        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>())).Throws(initException);
        var engine = CreateEngine(CreateProfile());

        // Act & Assert
        var ex = Assert.Throws<SimulationException>(() => engine.Run());
        Assert.Same(initException, ex.InnerException);
    }

    [Fact(DisplayName = "Run should throw SimulationException if an event's Execute method fails.")]
    public void Run_EventExecutionFails_ThrowsSimulationException()
    {
        // Arrange
        var eventException = new DivideByZeroException("Event failed!");
        var failingEvent = new Mock<AbstractEvent>();
        failingEvent.Setup(e => e.Execute(It.IsAny<IRunContext>())).Throws(eventException);

        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
                  .Callback<IRunContext>(ctx => ctx.Scheduler.Schedule(failingEvent.Object, 1.0));
        _mockStrategy.Setup(s => s.ShouldContinue(It.IsAny<IRunContext>())).Returns(true);

        var engine = CreateEngine(CreateProfile());

        // Act & Assert
        var ex = Assert.Throws<SimulationException>(() => engine.Run());
        Assert.Same(eventException, ex.InnerException);
    }

    [Fact(DisplayName = "Schedule should throw ArgumentOutOfRangeException if scheduled in the past.")]
    public void Schedule_TimeInThePast_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        // Run one event to advance the clock
        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
                  .Callback<IRunContext>(ctx => ctx.Scheduler.Schedule(new TestEvent(), 10.0));
        _mockStrategy.Setup(s => s.ShouldContinue(It.Is<IRunContext>(ctx => ctx.ExecutedEventCount < 1)))
                     .Returns(true);

        var engine = CreateEngine(CreateProfile());
        engine.Run(); // Clock is now 10.0

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>("time", () => engine.Schedule(new TestEvent(), 9.0));
    }

    [Fact(DisplayName = "Run should call tracer for scheduling, execution, and completion.")]
    public void Run_WithTracer_CallsTraceAtCorrectPoints()
    {
        // Arrange
        var testEvent = new TestEvent();
        _mockModel.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
                  .Callback<IRunContext>(ctx => ctx.Scheduler.Schedule(testEvent, 1.0));

        _mockStrategy.Setup(s => s.ShouldContinue(It.Is<IRunContext>(ctx => ctx.ExecutedEventCount < 1)))
                     .Returns(true);

        var engine = CreateEngine(CreateProfile());

        // Act
        engine.Run();

        // Assert
        _mockTracer.Verify(t => t.Trace(It.Is<TraceRecord>(r => r.Point == TracePoint.EventScheduled)), Times.Once);
        _mockTracer.Verify(t => t.Trace(It.Is<TraceRecord>(r => r.Point == TracePoint.EventExecuting)), Times.Once);
        _mockTracer.Verify(t => t.Trace(It.Is<TraceRecord>(r => r.Point == TracePoint.EventCompleted)), Times.Once);
    }
}
