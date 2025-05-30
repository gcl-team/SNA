using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;

namespace SimNextgenApp.Tests.Core;

public class ServerTests
{
    private const string DefaultServerName = "TestServer";
    private const int DefaultSeed = 456;

    private Mock<IScheduler> _mockScheduler;
    private Mock<IRunContext> _mockEngine;

    private double _currentTestTime;

    private ServerStaticConfig<DummyLoad> _defaultConfig;
    private Func<DummyLoad, Random, TimeSpan> _defaultServiceTimeFunc = (load, rnd) => TimeSpan.FromSeconds(10);

    public ServerTests()
    {
        _mockScheduler = new Mock<IScheduler>();

        _mockEngine = new Mock<IRunContext>();
        _mockEngine.As<IScheduler>().Setup(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<double>()));
        _mockEngine.SetupGet(e => e.ClockTime).Returns(() => _currentTestTime);
        _mockEngine.Setup(e => e.Scheduler).Returns(_mockScheduler.Object);

        _defaultConfig = new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 1 };
    }

    private Server<DummyLoad> CreateAndInitializeServer(
        ServerStaticConfig<DummyLoad>? config = null,
        int seed = DefaultSeed,
        string name = DefaultServerName)
    {
        var server = new Server<DummyLoad>(config ?? _defaultConfig, seed, name);
        server.Initialize(_mockScheduler.Object);
        return server;
    }

    private DummyLoad CreateDummyLoad(string? tag = null) => new DummyLoad(tag);

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitializeProperly()
    {
        var config = new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 2 };
        var server = new Server<DummyLoad>(config, seed: 123, instanceName: "TestServer");

        Assert.Equal(0, server.NumberInService);
        Assert.Equal(2, server.Vacancy);
        Assert.Empty(server.LoadsInService);
        Assert.Equal(0, server.LoadsCompletedCount);
        Assert.NotNull(server.BusyServerUnitsCounter);
        Assert.Equal(0.0, server.BusyServerUnitsCounter.InitialTime); // Default for TimeBasedMetric
        Assert.NotNull(server.ServiceStartTimes);
        Assert.NotNull(server.LoadDepartActions);
        Assert.NotNull(server.StateChangeActions);
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Server<DummyLoad>(null!, 123, "Invalid"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveOrZeroCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        var config = new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = invalidCapacity };
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Server<DummyLoad>(config, 123, "Invalid"));
        Assert.Equal("config", ex.ParamName); // Config parameter contains the invalid capacity
    }

    [Fact]
    public void Initialize_NullScheduler_ThrowsArgumentNullException()
    {
        var server = new Server<DummyLoad>(_defaultConfig, DefaultSeed, DefaultServerName); // Not initialized yet
        Assert.Throws<ArgumentNullException>(() => server.Initialize(null!));
    }

    [Fact]
    public void Initialize_SetsScheduler()
    {
        var server = CreateAndInitializeServer(); // Initialize is called inside
                                                  // Indirect assertion: TryStartService should now use the initialized scheduler
        _currentTestTime = 0;
        Assert.True(server.TryStartService(_mockEngine.Object, CreateDummyLoad()));
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<ServerStartServiceEvent<DummyLoad>>(), 0.0), Times.Once);
    }

    // --- Tests for TryStartService ---
    [Fact]
    public void TryStartService_BeforeInitialize_ThrowsInvalidOperationException()
    {
        var server = new Server<DummyLoad>(_defaultConfig, DefaultSeed, DefaultServerName); // Not initialized
        Assert.Throws<InvalidOperationException>(() => server.TryStartService(_mockEngine.Object, CreateDummyLoad()));
    }

    [Fact]
    public void TryStartService_NullEngine_ThrowsArgumentNullException()
    {
        var server = CreateAndInitializeServer();
        Assert.Throws<ArgumentNullException>(() => server.TryStartService(null!, CreateDummyLoad()));
    }

    [Fact]
    public void TryStartService_NullLoad_ThrowsArgumentNullException()
    {
        var server = CreateAndInitializeServer();
        _currentTestTime = 0.0;
        Assert.Throws<ArgumentNullException>(() => server.TryStartService(_mockEngine.Object, null!));
    }

    [Fact]
    public void TryStartService_WithVacancy_ReturnsTrueAndSchedulesStartServiceEvent()
    {
        // Arrange
        var server = CreateAndInitializeServer(config: new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 1 });
        var load = CreateDummyLoad();
        _currentTestTime = 10.0;

        // Act
        bool result = server.TryStartService(_mockEngine.Object, load);

        // Assert
        Assert.True(result);
        // Verify that the scheduler provided during Initialize was used
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<ServerStartServiceEvent<DummyLoad>>(ev => ev.LoadToServe == load && ev.OwningServer == server),
            10.0), Times.Once);
    }

    [Fact]
    public void TryStartService_NoVacancy_ReturnsFalseAndDoesNotSchedule()
    {
        // Arrange
        var server = CreateAndInitializeServer(config: new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 1 });
        var load1 = CreateDummyLoad("L1");
        _currentTestTime = 10.0;
        server.TryStartService(_mockEngine.Object, load1); // Fill capacity by scheduling
                                                                                   // Manually execute the event to occupy the server for this test's purpose
        var startEvent = new ServerStartServiceEvent<DummyLoad>(server, load1);
        startEvent.Execute(_mockEngine.Object);
        _mockScheduler.ResetCalls(); // Reset calls after filling capacity

        var load2 = CreateDummyLoad("L2");
        _currentTestTime = 11.0;


        // Act
        bool result = server.TryStartService(_mockEngine.Object, load2);

        // Assert
        Assert.False(result);
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<double>()), Times.Never);
    }

    // --- Tests for Event Execution Logic ---
    [Fact]
    public void ServerStartServiceEvent_Execute_UpdatesStateAndSchedulesCompletion()
    {
        // Arrange
        var serviceDuration = TimeSpan.FromSeconds(5);
        var config = new ServerStaticConfig<DummyLoad>((l, r) => serviceDuration) { Capacity = 1 };
        var server = CreateAndInitializeServer(config: config);
        var load = CreateDummyLoad("L1");
        _currentTestTime = 20.0;

        List<double> stateChangeTimes = [];
        server.StateChangeActions.Add(time => stateChangeTimes.Add(time));

        var startServiceEvent = new ServerStartServiceEvent<DummyLoad>(server, load);

        AbstractEvent? scheduledCompletionEvent = null;
        // Setup the mock engine's scheduler for verification
        _mockScheduler
            .Setup(s => s.Schedule(It.IsAny<ServerServiceCompleteEvent<DummyLoad>>(), _currentTestTime + serviceDuration.TotalSeconds))
            .Callback<AbstractEvent, double>((ev, time) => scheduledCompletionEvent = ev)
            .Verifiable();

        // Act
        startServiceEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.Contains(load, server.LoadsInService);
        Assert.Equal(1, server.NumberInService);
        Assert.Equal(0, server.Vacancy);
        Assert.True(server.ServiceStartTimes.ContainsKey(load));
        Assert.Equal(20.0, server.ServiceStartTimes[load]);
        Assert.Equal(1, server.BusyServerUnitsCounter.CurrentCount);
        Assert.Single(stateChangeTimes);
        Assert.Equal(20.0, stateChangeTimes[0]);

        _mockScheduler.As<IScheduler>().Verify();
        Assert.NotNull(scheduledCompletionEvent);
        var completionEvent = Assert.IsType<ServerServiceCompleteEvent<DummyLoad>>(scheduledCompletionEvent);
        Assert.Same(load, completionEvent.ServedLoad);
    }

    [Fact]
    public void ServerServiceCompleteEvent_Execute_UpdatesStateAndInvokesDepartureActions()
    {
        // Arrange
        var server = CreateAndInitializeServer(); // Capacity 1
        var load = CreateDummyLoad("L1");
        _currentTestTime = 20.0;

        // Simulate load started service earlier
        server.LoadsInService.Add(load);
        server.ServiceStartTimes[load] = 10.0;
        server.BusyServerUnitsCounter.ObserveChange(1, 10.0); // Server became busy

        List<double> stateChangeTimes = new List<double>();
        server.StateChangeActions.Add(time => stateChangeTimes.Add(time));
        List<(DummyLoad load, double time)> departedLoadsInfo = new();
        server.LoadDepartActions.Add((l, t) => departedLoadsInfo.Add((l, t)));

        var serviceCompleteEvent = new ServerServiceCompleteEvent<DummyLoad>(server, load);
        _currentTestTime = 25.0; // Time of completion

        // Act
        serviceCompleteEvent.Execute(_mockEngine.Object);

        // Assert
        Assert.DoesNotContain(load, server.LoadsInService);
        Assert.Equal(0, server.NumberInService);
        Assert.Equal(1, server.Vacancy);
        Assert.False(server.ServiceStartTimes.ContainsKey(load));
        Assert.Equal(1, server.LoadsCompletedCount);
        Assert.Equal(0, server.BusyServerUnitsCounter.CurrentCount); // Server becomes idle

        Assert.Single(departedLoadsInfo);
        Assert.Same(load, departedLoadsInfo[0].load);
        Assert.Equal(25.0, departedLoadsInfo[0].time);

        Assert.Single(stateChangeTimes);
        Assert.Equal(25.0, stateChangeTimes[0]);
    }

    // --- Test for WarmedUp ---
    [Fact]
    public void WarmedUp_ResetsCountersAndObservesCurrentInService()
    {
        // Arrange
        var server = CreateAndInitializeServer(config: new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 2 });
        var load1 = CreateDummyLoad("L1");
        var load2 = CreateDummyLoad("L2");

        _currentTestTime = 50.0;
        // Manually put server in a state with 2 items in service
        server.LoadsInService.Add(load1);
        server.ServiceStartTimes[load1] = 40.0;
        server.LoadsInService.Add(load2);
        server.ServiceStartTimes[load2] = 45.0;
        server.BusyServerUnitsCounter.ObserveCount(2, _currentTestTime); // Force metric to count 2

        server.PerformLoadCompletedCountUpdate(10);

        double warmUpTime = 100.0;

        // Act
        server.WarmedUp(warmUpTime);

        // Assert
        Assert.Equal(0, server.LoadsCompletedCount);
        Assert.Equal(warmUpTime, server.BusyServerUnitsCounter.InitialTime);
        Assert.Equal(warmUpTime, server.BusyServerUnitsCounter.CurrentTime);
        // With the refinement in Server.WarmedUp to call ObserveCount:
        Assert.Equal(2, server.BusyServerUnitsCounter.CurrentCount);
        Assert.Equal(2, server.NumberInService); // LoadsInService should be untouched by WarmedUp itself
    }

    [Fact]
    public void WarmedUp_ServerEmpty_CorrectlyResetsMetric()
    {
        // Arrange
        var server = CreateAndInitializeServer();
        server.PerformLoadCompletedCountUpdate(5);
        _currentTestTime = 50.0;
        // Ensure metric has some initial state if needed, or just ensure server is empty
        Assert.Empty(server.LoadsInService);
        server.BusyServerUnitsCounter.ObserveCount(0, _currentTestTime);


        double warmUpTime = 100.0;

        // Act
        server.WarmedUp(warmUpTime);

        // Assert
        Assert.Equal(0, server.LoadsCompletedCount);
        Assert.Equal(warmUpTime, server.BusyServerUnitsCounter.InitialTime);
        Assert.Equal(warmUpTime, server.BusyServerUnitsCounter.CurrentTime);
        Assert.Equal(0, server.BusyServerUnitsCounter.CurrentCount); // Stays 0 because LoadsInService is empty
    }
}