namespace SimNextgenApp.Tests.Observability;

public class SpanHierarchyIntegrationTests
{
    [Fact]
    public void SimulationRun_WithWarmup_CreatesWarmupSpan()
    {
        using var telemetry = SimulationTelemetry.Create().WithConsoleExporter().Build();
        var model = new SimpleEventCountingModel(totalEvents: 10);
        var strategy = new EventCountRunStrategy(maxEventCount: 100, warmupEndTime: 5);
        var profile = new SimulationProfile(model: model, runStrategy: strategy, telemetry: telemetry);
        var engine = new SimulationEngine(profile);

        var result = engine.Run();

        Assert.Equal(10, result.ExecutedEventCount);
    }

    [Fact]
    public void SimulationRun_WithoutWarmup_DoesNotCreateWarmupSpan()
    {
        using var telemetry = SimulationTelemetry.Create().WithConsoleExporter().Build();
        var model = new SimpleEventCountingModel(totalEvents: 10);
        var strategy = new EventCountRunStrategy(maxEventCount: 100);
        var profile = new SimulationProfile(model: model, runStrategy: strategy, telemetry: telemetry);
        var engine = new SimulationEngine(profile);

        var result = engine.Run();

        Assert.Equal(10, result.ExecutedEventCount);
    }

    [Fact]
    public void SimulationRun_WithCardinalityGuard_TracksEventTypes()
    {
        using var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .WithCardinalityGuard(threshold: 100)
            .Build();

        var model = new SimpleEventCountingModel(totalEvents: 10);
        var strategy = new EventCountRunStrategy(maxEventCount: 100);
        var profile = new SimulationProfile(model: model, runStrategy: strategy, telemetry: telemetry);
        var engine = new SimulationEngine(profile);

        var result = engine.Run();

        Assert.Equal(10, result.ExecutedEventCount);
        Assert.NotNull(telemetry.CardinalityGuard);
        var stats = telemetry.CardinalityGuard.GetStatistics();
        Assert.True(stats.TotalUniqueValues > 0);
    }
}

internal class SimpleEventCountingModel : ISimulationModel
{
    private readonly int _totalEvents;
    private static long _nextId = 1;

    public SimpleEventCountingModel(int totalEvents)
    {
        _totalEvents = totalEvents;
        Id = Interlocked.Increment(ref _nextId);
        Metadata = new Dictionary<string, object>();
    }

    public long Id { get; }
    public string Name => "SimpleEventCountingModel";
    public IReadOnlyDictionary<string, object> Metadata { get; }

    public void Initialize(IRunContext context)
    {
        for (int i = 1; i <= _totalEvents; i++)
        {
            context.Scheduler.Schedule(new TestEvent(), time: i);
        }
    }
}

internal class TestEvent : AbstractEvent
{
    public override void Execute(IRunContext context) { }
}
