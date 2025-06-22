using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Exceptions;
using SimNextgenApp.Modeling;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimNextgenApp.Tests.Core;

public class SimulationEngineTests
{
    private readonly Mock<ILogger<SimulationEngine>> _mockLogger = new();

    private SimulationProfile CreateTestProfile(ISimulationModel model = null, IRunStrategy strategy = null)
    {
        return new SimulationProfile(
            model ?? new Mock<ISimulationModel>().Object,
            strategy ?? Mock.Of<IRunStrategy>(s => s.ShouldContinue(It.IsAny<IRunContext>()) == false),
            "TestProfile",
            SimulationTimeUnit.Seconds,
            NullLoggerFactory.Instance
        );
    }

    [Fact]
    public void Run_InitializesModelAndExecutesEvents()
    {
        // Arrange
        var modelMock = new Mock<ISimulationModel>();
        var strategyMock = new Mock<IRunStrategy>();

        var testEvent = new TestEvent();

        modelMock.Setup(m => m.Initialize(It.IsAny<IScheduler>()))
                 .Callback<IScheduler>(sched => sched.Schedule(testEvent, 1.0));

        strategyMock.SetupSequence(s => s.ShouldContinue(It.IsAny<IRunContext>()))
                    .Returns(true)
                    .Returns(false);

        var profile = CreateTestProfile(modelMock.Object, strategyMock.Object);
        var engine = new SimulationEngine(profile);

        // Act
        engine.Run();

        // Assert
        Assert.True(testEvent.Executed);
        Assert.Equal(1.0, engine.ClockTime);
        modelMock.Verify(m => m.Initialize(It.IsAny<IScheduler>()), Times.Once);
    }

    [Fact]
    public void Run_CallsWarmedUp_WhenWarmupTimeReached()
    {
        // Arrange
        var modelMock = new Mock<ISimulationModel>();
        var warmupAwareMock = modelMock.As<IWarmupAware>();
        var strategyMock = new Mock<IRunStrategy>();

        var warmupTime = 5.0;
        var testEvent = new TestEvent();

        modelMock.Setup(m => m.Initialize(It.IsAny<IScheduler>()))
                 .Callback<IScheduler>(sched => sched.Schedule(testEvent, warmupTime));

        strategyMock.SetupGet(s => s.WarmupEndTime).Returns(warmupTime);
        strategyMock.SetupSequence(s => s.ShouldContinue(It.IsAny<IRunContext>()))
                    .Returns(true)          // Continue to process the event
                    .Returns(false);        // Stop after the event

        var profile = CreateTestProfile(modelMock.Object, strategyMock.Object);
        var engine = new SimulationEngine(profile);

        // Act
        engine.Run();

        // Assert
        Assert.True(testEvent.Executed);
        warmupAwareMock.Verify(m => m.WarmedUp(warmupTime), Times.Once);
    }

    [Fact]
    public void Run_ThrowsIfRunTwice()
    {
        var modelMock = new Mock<ISimulationModel>();
        var strategyMock = new Mock<IRunStrategy>();

        strategyMock.SetupSequence(s => s.ShouldContinue(It.IsAny<IRunContext>()))
                    .Returns(false); // No event run

        var profile = CreateTestProfile(modelMock.Object, strategyMock.Object);
        var engine = new SimulationEngine(profile);

        // First run
        engine.Run();

        // Second run should fail
        Assert.Throws<InvalidOperationException>(() => engine.Run());
    }

    [Fact]
    public void Schedule_SetsExecutionTimeCorrectly()
    {
        var testEvent = new TestEvent();
        var engine = new SimulationEngine(CreateTestProfile());

        engine.Schedule(testEvent, 7.0);

        Assert.Equal(7.0, testEvent.ExecutionTime);
    }

    [Fact]
    public void Schedule_UsingDelay_QueuesCorrectly()
    {
        var testEvent = new TestEvent();
        var engine = new SimulationEngine(CreateTestProfile());

        engine.Schedule(testEvent, TimeSpan.FromSeconds(10));

        Assert.Equal(10.0, testEvent.ExecutionTime);
    }

    [Fact]
    public void Run_ExecutesEventsInCorrectOrder()
    {
        var executedEvents = new List<string>();

        var e1 = new NamedEvent("A", executedEvents);
        var e2 = new NamedEvent("B", executedEvents);
        var e3 = new NamedEvent("C", executedEvents);

        var modelMock = new Mock<ISimulationModel>();
        modelMock.Setup(m => m.Initialize(It.IsAny<IScheduler>()))
            .Callback<IScheduler>(s =>
            {
                s.Schedule(e3, 3.0);
                s.Schedule(e1, 1.0);
                s.Schedule(e2, 2.0);
            });

        var strategyMock = new Mock<IRunStrategy>();
        strategyMock.Setup(s => s.ShouldContinue(It.IsAny<IRunContext>()))
            .Returns(true);

        var profile = CreateTestProfile(modelMock.Object, strategyMock.Object);
        var engine = new SimulationEngine(profile);

        engine.Run();

        Assert.Equal(new[] { "A", "B", "C" }, executedEvents);
    }
}
