using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;
using SimNextgenApp.Exceptions;
using SimNextgenApp.Modeling.Server;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Modeling.Server;

public class ServerTests
{
    private const string DefaultServerName = "TestServer";
    private const int DefaultSeed = 456;

    private Mock<IScheduler> _mockScheduler;
    private readonly Mock<IRunContext> _mockContext;
    private double _currentTestTime;

    private readonly ServerStaticConfig<DummyLoad> _defaultConfig;
    private readonly Func<DummyLoad, Random, TimeSpan> _defaultServiceTimeFunc = (load, rnd) => TimeSpan.FromSeconds(10);

    public ServerTests()
    {
        _mockScheduler = new Mock<IScheduler>();

        _mockContext = new Mock<IRunContext>();
        _mockContext.SetupGet(e => e.ClockTime).Returns(() => _currentTestTime);
        _mockContext.SetupGet(e => e.Scheduler).Returns(_mockScheduler.Object);

        _defaultConfig = new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 1 };
    }

    private Server<DummyLoad> CreateServer(
        ServerStaticConfig<DummyLoad>? config = null,
        int seed = DefaultSeed,
        string name = DefaultServerName)
    {
        var server = new Server<DummyLoad>(config ?? _defaultConfig, seed, name);
        
        return server;
    }

    private DummyLoad CreateDummyLoad(string? tag = null) => new(tag);

    [Fact(DisplayName = "Constructor should initialise properties correctly based on config.")]
    public void Constructor_WithValidConfig_InitializesProperties()
    {
        // Arrange
        var config = new ServerStaticConfig<DummyLoad>(_defaultServiceTimeFunc) { Capacity = 2 };

        // Act
        var server = new Server<DummyLoad>(config, seed: 123, instanceName: "TestServer");

        // Assert
        Assert.Equal(2, server.Capacity);
        Assert.Equal(0, server.NumberInService);
        Assert.Equal(2, server.Vacancy);
        Assert.Empty(server.LoadsInService);
        Assert.NotNull(server.ServiceStartTimes);
    }

    [Fact(DisplayName = "Constructor should throw ArgumentNullException if config is null.")]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Server<DummyLoad>(null!, 123, "Invalid"));
    }

    [Fact(DisplayName = "TryStartService should succeed and update state if there is vacancy.")]
    public void TryStartService_WithVacancy_ReturnsTrueAndSchedulesCompletion()
    {
        // Arrange
        var server = CreateServer();
        var load = CreateDummyLoad();
        _currentTestTime = 10.0;

        bool isStateChangedFired = false;
        server.StateChanged += _ => isStateChangedFired = true;

        // Act
        bool result = server.TryStartService(load, _mockContext.Object);

        // Assert
        Assert.True(result);

        // 1. Verify internal state change
        Assert.Contains(load, server.LoadsInService);
        Assert.Equal(1, server.NumberInService);
        Assert.Equal(10.0, server.ServiceStartTimes[load]);

        // 2. Verify scheduling of the correct future event
        _mockScheduler.Verify(s => s.Schedule(
            It.Is<ServerServiceCompleteEvent<DummyLoad>>(ev => ev.ServedLoad == load),
            TimeSpan.FromSeconds(10)), Times.Once);

        // 3. Verify event hook was fired
        Assert.True(isStateChangedFired);
    }

    [Fact(DisplayName = "TryStartService should fail and not change state if there is no vacancy.")]
    public void TryStartService_NoVacancy_ReturnsFalseAndDoesNotSchedule()
    {
        // Arrange
        var server = CreateServer();
        var load1 = CreateDummyLoad("L1");
        _currentTestTime = 10.0;
        server.TryStartService(load1, _mockContext.Object); // Fill the one slot of capacity

        // Reset mocks and event listeners to ensure we only test the second call.
        _mockScheduler.Invocations.Clear();
        bool isStateChangedFired = false;
        server.StateChanged += time => isStateChangedFired = true;

        var load2 = CreateDummyLoad("L2");
        _currentTestTime = 11.0;

        // Act
        bool result = server.TryStartService(load2, _mockContext.Object);

        // Assert
        Assert.False(result);
        Assert.DoesNotContain(load2, server.LoadsInService); // State should be unchanged
        _mockScheduler.Verify(s => s.Schedule(It.IsAny<AbstractEvent>(), It.IsAny<TimeSpan>()), Times.Never); // No events scheduled
        Assert.False(isStateChangedFired); // No state change event fired
    }

    [Fact(DisplayName = "TryStartService should throw ArgumentNullException for a null run context.")]
    public void TryStartService_NullLoad_ThrowsArgumentNullException()
    {
        // Arrange
        var server = CreateServer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>("loadToServe", () => server.TryStartService(null!, _mockContext.Object));
    }

    [Fact(DisplayName = "TryStartService should throw ArgumentNullException for a null run context.")]
    public void TryStartService_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var server = CreateServer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>("engineContext", () => server.TryStartService(new DummyLoad(), null!));
    }

    [Fact(DisplayName = "HandleServiceCompletion should update state and fire events for a valid load.")]
    public void HandleServiceCompletion_ValidLoad_UpdatesStateAndFiresEvents()
    {
        // Arrange
        var server = CreateServer();
        var load = CreateDummyLoad("L1");

        // Manually put the server in a state as if a load was being served.
        // We do this because we are testing the internal HandleServiceCompletion method,
        // which is called by the ServerServiceCompleteEvent.
        server._loadsInService.Add(load);
        server._serviceStartTimes[load] = 10.0;

        bool isStateChangedFired = false;
        bool isDepartureFired = false;
        (DummyLoad? departedLoad, double departureTime) departureInfo = (null, -1.0);

        server.StateChanged += time => isStateChangedFired = true;
        server.LoadDeparted += (l, t) =>
        {
            isDepartureFired = true;
            departureInfo = (l, t);
        };

        _currentTestTime = 25.0; // Time of completion

        // Act
        server.HandleServiceCompletion(load, _currentTestTime);

        // Assert
        // 1. Verify internal state is cleared
        Assert.DoesNotContain(load, server.LoadsInService);
        Assert.Equal(0, server.NumberInService);
        Assert.Equal(1, server.Vacancy);
        Assert.False(server.ServiceStartTimes.ContainsKey(load));

        // 2. Verify events were fired
        Assert.True(isStateChangedFired);
        Assert.True(isDepartureFired);
        Assert.Same(load, departureInfo.departedLoad);
        Assert.Equal(25.0, departureInfo.departureTime);
    }

    [Fact(DisplayName = "HandleServiceCompletion should throw SimulationException for a non-existent load.")]
    public void HandleServiceCompletion_NonExistentLoad_ThrowsSimulationException()
    {
        // Arrange
        var server = CreateServer();
        var nonExistentLoad = new DummyLoad();

        // Act & Assert
        var ex = Assert.Throws<SimulationException>(() => server.HandleServiceCompletion(nonExistentLoad, 10.0));
        Assert.Contains("which was not in service", ex.Message);
    }

    [Fact(DisplayName = "WarmedUp should reset service start times for loads currently in service.")]
    public void WarmedUp_WithInFlightLoads_ResetsTheirStartTimes()
    {
        // Arrange
        var server = CreateServer(config: new ServerStaticConfig<DummyLoad>((_,_) => TimeSpan.Zero) { Capacity = 2 });
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