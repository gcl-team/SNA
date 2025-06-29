using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;
using SimNextgenApp.Modeling;

namespace SimNextgenApp.Tests.Modeling;

public class ServerTests
{
    private const string DefaultServerName = "TestServer";
    private const int DefaultSeed = 456;

    private Mock<IScheduler> _mockScheduler;
    private double _currentTestTime;

    private readonly ServerStaticConfig<DummyLoad> _defaultConfig;
    private readonly Func<DummyLoad, Random, TimeSpan> _defaultServiceTimeFunc = (load, rnd) => TimeSpan.FromSeconds(10);

    public ServerTests()
    {
        _mockScheduler = new Mock<IScheduler>();

        _mockScheduler.SetupGet(s => s.ClockTime).Returns(() => _currentTestTime);

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

    private DummyLoad CreateDummyLoad(string? tag = null) => new(tag);

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitializeProperly()
    {
        var config = new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 2 };
        var server = new Server<DummyLoad>(config, seed: 123, instanceName: "TestServer");

        Assert.Equal(0, server.NumberInService);
        Assert.Equal(2, server.Vacancy);
        Assert.Empty(server.LoadsInService);
        Assert.NotNull(server.ServiceStartTimes);
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
    public void TryStartService_BeforeInitialize_ThrowsInvalidOperationException()
    {
        var server = new Server<DummyLoad>(_defaultConfig, DefaultSeed, DefaultServerName); // Not initialized yet
        Assert.Throws<InvalidOperationException>(() => server.TryStartService(CreateDummyLoad()));
    }

    [Fact]
    public void TryStartService_NullLoad_ThrowsArgumentNullException()
    {
        var server = CreateAndInitializeServer();
        _currentTestTime = 0.0;
        Assert.Throws<ArgumentNullException>(() => server.TryStartService(null!));
    }

    [Fact]
    public void TryStartService_WithVacancy_ReturnsTrueAndSchedulesStartServiceEvent()
    {
        // Arrange
        var serviceDuration = TimeSpan.FromSeconds(5);
        var config = new ServerStaticConfig<DummyLoad>((l, r) => serviceDuration) { Capacity = 1 };
        var server = CreateAndInitializeServer(config: config);
        var load = CreateDummyLoad();
        _currentTestTime = 10.0;

        bool wasStateChangedFired = false;
        server.StateChanged += time => wasStateChangedFired = true;

        // Act
        bool result = server.TryStartService(load);

        // Assert
        Assert.True(result);

        // 1. Verify internal state change
        Assert.Contains(load, server.LoadsInService);
        Assert.Equal(1, server.NumberInService);
        Assert.Equal(10.0, server.ServiceStartTimes[load]);

        // 2. Verify scheduling of the correct future event
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<ServerServiceCompleteEvent<DummyLoad>>(ev => ev.ServedLoad == load),
            serviceDuration), Times.Once);

        // 3. Verify event hook was fired
        Assert.True(wasStateChangedFired);
    }

    [Fact]
    public void TryStartService_NoVacancy_ReturnsFalseAndDoesNotSchedule()
    {
        // Arrange
        var server = CreateAndInitializeServer(config: new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 1 });
        var load1 = CreateDummyLoad("L1");
        _currentTestTime = 10.0;
        server.TryStartService(load1); // Fill the one slot of capacity

        // Reset mocks and event listeners to ensure we only test the second call.
        _mockScheduler.Invocations.Clear();
        bool wasStateChangedFired = false;
        server.StateChanged += time => wasStateChangedFired = true;

        var load2 = CreateDummyLoad("L2");
        _currentTestTime = 11.0;

        // Act
        bool result = server.TryStartService(load2);

        // Assert
        Assert.False(result);
        Assert.DoesNotContain(load2, server.LoadsInService); // State should be unchanged
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<TimeSpan>()), Times.Never); // No events scheduled
        Assert.False(wasStateChangedFired); // No state change event fired
    }

    [Fact]
    public void HandleServiceCompletion_UpdatesStateAndInvokesDepartureEvents()
    {
        // Arrange
        var server = CreateAndInitializeServer(); // Capacity 1
        var load = CreateDummyLoad("L1");

        // Manually put the server in a state as if a load was being served.
        // We do this because we are testing the internal HandleServiceCompletion method,
        // which is called by the ServerServiceCompleteEvent.
        server._loadsInService.Add(load);
        server._serviceStartTimes[load] = 10.0;

        bool wasStateChangedFired = false;
        bool wasDepartureFired = false;
        (DummyLoad? departedLoad, double departureTime) departureInfo = (null, -1.0);

        // REFACTOR: Subscribing to C# events
        server.StateChanged += time => wasStateChangedFired = true;
        server.LoadDeparted += (l, t) =>
        {
            wasDepartureFired = true;
            departureInfo = (l, t);
        };

        _currentTestTime = 25.0; // Time of completion

        // Act
        // We directly invoke the internal method to unit test its logic in isolation.
        server.HandleServiceCompletion(load, _currentTestTime);

        // Assert
        // 1. Verify internal state is cleared
        Assert.DoesNotContain(load, server.LoadsInService);
        Assert.Equal(0, server.NumberInService);
        Assert.Equal(1, server.Vacancy);
        Assert.False(server.ServiceStartTimes.ContainsKey(load));

        // 2. Verify events were fired
        Assert.True(wasStateChangedFired);
        Assert.True(wasDepartureFired);
        Assert.Same(load, departureInfo.departedLoad);
        Assert.Equal(25.0, departureInfo.departureTime);
    }

    [Fact]
    public void WarmedUp_ResetsServiceStartTimesForInFlightLoads()
    {
        // Arrange
        var server = CreateAndInitializeServer();
        var load1 = new DummyLoad("L1");
        var load2 = new DummyLoad("L2");

        // Manually put the server in a state with 2 loads that started service before warm-up.
        server._loadsInService.Add(load1);
        server._loadsInService.Add(load2);
        server._serviceStartTimes[load1] = 40.0;
        server._serviceStartTimes[load2] = 45.0;

        double warmUpTime = 100.0;

        // Act
        server.WarmedUp(warmUpTime);

        // Assert
        Assert.Equal(2, server.NumberInService);
        Assert.Equal(warmUpTime, server.ServiceStartTimes[load1]);
        Assert.Equal(warmUpTime, server.ServiceStartTimes[load2]);
    }
}