using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;

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
        server.TryStartService(_mockEngine.Object, load1);

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
        var load = new DummyLoad("L1");
        _currentTestTime = 20.0;

        bool wasStateChangeFired = false;
        server.StateChangeActions.Add(time => wasStateChangeFired = true);

        var startServiceEvent = new ServerStartServiceEvent<DummyLoad>(server, load);

        AbstractEvent? scheduledCompletionEvent = null;

        _mockScheduler
            .Setup(s => s.Schedule(It.IsAny<ServerServiceCompleteEvent<DummyLoad>>(), serviceDuration))
            .Callback<AbstractEvent, TimeSpan>((ev, delay) => scheduledCompletionEvent = ev)
            .Verifiable();

        // Act
        startServiceEvent.Execute(_mockEngine.Object);

        // Assert
        // 1. Verify internal state
        Assert.Contains(load, server.LoadsInService);
        Assert.Equal(1, server.NumberInService);
        Assert.Equal(20.0, server.ServiceStartTimes[load]);

        // 2. Verify signal was fired
        Assert.True(wasStateChangeFired);

        // 3. Verify the scheduling call
        _mockScheduler.Verify(); // This will now pass!
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

        bool wasStateChangeFired = false;
        bool wasDepartureFired = false;
        (DummyLoad? departedLoad, double departureTime) departureInfo = (null, -1.0);

        server.StateChangeActions.Add(time => wasStateChangeFired = true);
        server.LoadDepartActions.Add((l, t) =>
        {
            wasDepartureFired = true;
            departureInfo = (l, t);
        });

        var serviceCompleteEvent = new ServerServiceCompleteEvent<DummyLoad>(server, load);
        _currentTestTime = 25.0; // Time of completion

        // Act
        serviceCompleteEvent.Execute(_mockEngine.Object);

        // Assert
        // 1. Verify the server's internal state has been cleared
        Assert.DoesNotContain(load, server.LoadsInService);
        Assert.Equal(0, server.NumberInService);
        Assert.Equal(1, server.Vacancy);
        Assert.False(server.ServiceStartTimes.ContainsKey(load), "ServiceStartTimes should be cleaned up after departure.");

        // 2. Verify the server's primary output: its signals
        Assert.True(wasStateChangeFired, "The server should have fired a general state change signal.");

        Assert.True(wasDepartureFired, "The server should have fired a specific load departure signal.");
        Assert.Same(load, departureInfo.departedLoad);
        Assert.Equal(25.0, departureInfo.departureTime);
    }

    [Fact]
    public void Server_WarmedUp_ResetsServiceStartTimesForInFlightLoads()
    {
        // Arrange
        var server = CreateAndInitializeServer(); // Capacity doesn't matter much here
        var load1 = new DummyLoad("L1");
        var load2 = new DummyLoad("L2");

        // Manually put the server in a state with 2 loads that started service before warm-up.
        server.LoadsInService.Add(load1);
        server.ServiceStartTimes[load1] = 40.0; // Started at T=40
        server.LoadsInService.Add(load2);
        server.ServiceStartTimes[load2] = 45.0; // Started at T=45

        double warmUpTime = 100.0;

        // Act
        server.WarmedUp(warmUpTime);

        // Assert
        // 1. Verify that the loads are still in service.
        Assert.Equal(2, server.NumberInService);
        Assert.Contains(load1, server.LoadsInService);
        Assert.Contains(load2, server.LoadsInService);

        // 2. Verify the ONLY responsibility of Server.WarmedUp:
        //    The start times for the in-flight loads have been updated to the warm-up time.
        Assert.Equal(warmUpTime, server.ServiceStartTimes[load1]);
        Assert.Equal(warmUpTime, server.ServiceStartTimes[load2]);
    }
}