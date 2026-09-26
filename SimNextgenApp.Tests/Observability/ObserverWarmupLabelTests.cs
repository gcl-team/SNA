using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Events;
using SimNextgenApp.Modeling;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Modeling.Resource;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Observability;

namespace SimNextgenApp.Tests.Observability;

/// <summary>
/// Observers label their metrics with the engine's warmup state, which does not depend on tracing.
/// None of these tests start a span.
/// </summary>
public class ObserverWarmupLabelTests
{
    [Theory(DisplayName = "QueueObserver should label metrics with the engine's warmup state.")]
    [InlineData(true)]
    [InlineData(false)]
    public void QueueObserver_LabelsMetricsWithWarmupState(bool isWarmup)
    {
        // Arrange
        var queue = new Mock<ISimQueue<DummyLoad>>();
        queue.SetupGet(q => q.Name).Returns("TestQueue");
        using var observer = QueueObserver.CreateSimple(queue.Object);
        using var recorder = new WarmupLabelRecorder(observer.Meter!);

        // Act
        using (new WarmupPhaseScope(isWarmup))
        {
            queue.Raise(q => q.LoadEnqueued += null, new DummyLoad(), 5L);
        }

        // Assert
        Assert.Equal<bool?>([isWarmup], recorder.LabelsFor("sna.queue.loads_enqueued"));
    }

    [Theory(DisplayName = "ServerObserver should label metrics with the engine's warmup state.")]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerObserver_LabelsMetricsWithWarmupState(bool isWarmup)
    {
        // Arrange
        var server = new Mock<IServer<DummyLoad>>();
        server.SetupGet(s => s.Name).Returns("TestServer");
        using var observer = ServerObserver.CreateSimple(server.Object);
        using var recorder = new WarmupLabelRecorder(observer.Meter!);

        // Act
        using (new WarmupPhaseScope(isWarmup))
        {
            server.Raise(s => s.LoadDeparted += null, new DummyLoad(), 5L);
        }

        // Assert
        Assert.Equal<bool?>([isWarmup], recorder.LabelsFor("sna.server.loads_completed"));
    }

    [Theory(DisplayName = "GeneratorObserver should label metrics with the engine's warmup state.")]
    [InlineData(true)]
    [InlineData(false)]
    public void GeneratorObserver_LabelsMetricsWithWarmupState(bool isWarmup)
    {
        // Arrange
        var generator = new Mock<IGenerator<DummyLoad>>();
        generator.SetupGet(g => g.Name).Returns("TestGenerator");
        using var observer = GeneratorObserver.CreateSimple(generator.Object);
        using var recorder = new WarmupLabelRecorder(observer.Meter!);

        // Act
        using (new WarmupPhaseScope(isWarmup))
        {
            generator.Raise(g => g.LoadGenerated += null, new DummyLoad(), 5L);
        }

        // Assert
        Assert.Equal<bool?>([isWarmup], recorder.LabelsFor("sna.generator.loads_generated"));
    }

    [Theory(DisplayName = "ResourceObserver should label metrics with the engine's warmup state.")]
    [InlineData(true)]
    [InlineData(false)]
    public void ResourceObserver_LabelsMetricsWithWarmupState(bool isWarmup)
    {
        // Arrange
        var pool = new Mock<IResourcePool<DummyLoad>>();
        pool.SetupGet(p => p.Name).Returns("TestPool");
        using var observer = ResourceObserver.CreateSimple(pool.Object);
        using var recorder = new WarmupLabelRecorder(observer.Meter!);

        // Act
        using (new WarmupPhaseScope(isWarmup))
        {
            pool.Raise(p => p.ResourceAcquired += null, new DummyLoad(), 5L);
        }

        // Assert
        Assert.Equal<bool?>([isWarmup], recorder.LabelsFor("sna.resource.acquisitions"));
    }

    [Fact(DisplayName = "Observers should label metrics by warmup phase in a run without tracing.")]
    public void Run_WithoutTracing_LabelsObserverMetricsByWarmupPhase()
    {
        // Arrange
        var server = new Mock<IServer<DummyLoad>>();
        server.SetupGet(s => s.Name).Returns("TestServer");
        using var observer = ServerObserver.CreateSimple(server.Object);
        using var recorder = new WarmupLabelRecorder(observer.Meter!);

        void Depart(IRunContext context) => server.Raise(s => s.LoadDeparted += null, new DummyLoad(), context.ClockTime);

        var model = new Mock<ISimulationModel>();
        model.Setup(m => m.Initialize(It.IsAny<IRunContext>()))
             .Callback<IRunContext>(ctx =>
             {
                 ctx.Scheduler.Schedule(new CallbackEvent(Depart), 3);  // During warmup
                 ctx.Scheduler.Schedule(new CallbackEvent(Depart), 10); // Warmup ends at this event
                 ctx.Scheduler.Schedule(new CallbackEvent(Depart), 12); // After warmup
             });
        var strategy = new Mock<IRunStrategy>();
        strategy.SetupGet(s => s.WarmupEndTime).Returns(10);
        strategy.Setup(s => s.ShouldContinue(It.IsAny<IRunContext>())).Returns(true);

        var profile = new SimulationProfile(
            model.Object, strategy.Object, "NoTracing", SimulationTimeUnit.Seconds, NullLoggerFactory.Instance, telemetry: null);

        // Act
        new SimulationEngine(profile).Run();

        // Assert
        Assert.Equal<bool?>([true, false, false], recorder.LabelsFor("sna.server.loads_completed"));
    }

    private sealed class CallbackEvent(Action<IRunContext> callback) : AbstractEvent
    {
        public override void Execute(IRunContext engine) => callback(engine);
    }
}
