using Moq;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Statistics;
using System.ComponentModel;

namespace SimNextgenApp.Tests.Statistics;

public class ServerObserverTests
{
    private readonly Mock<IServer<DummyLoad>> _mockServer;
    private readonly ServerObserver<DummyLoad> _observer;
    private readonly DummyLoad _testLoad = new();

    public ServerObserverTests()
    {
        // Use Strict mock behavior to ensure we are setting up all necessary interactions.
        _mockServer = new Mock<IServer<DummyLoad>>(MockBehavior.Strict);

        // The observer needs to subscribe to events. Moq requires us to set up
        // the event add/remove accessors for Strict mocks.
        _mockServer.SetupAdd(s => s.StateChanged += It.IsAny<Action<double>>());
        _mockServer.SetupAdd(s => s.LoadDeparted += It.IsAny<Action<DummyLoad, double>>());

        _observer = new ServerObserver<DummyLoad>(_mockServer.Object);
    }

    [Fact]
    [DisplayName("Constructor should throw ArgumentNullException if the server is null.")]
    public void Constructor_WithNullServer_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => 
            new ServerObserver<DummyLoad>(null!)
        );

        Assert.Equal("serverToObserve", ex.ParamName);
    }

    [Fact]
    [DisplayName("Constructor should subscribe to server events upon creation.")]
    public void Constructor_WithValidServer_SubscribesToEvents()
    {
        // Arrange
        var localMockServer = new Mock<IServer<DummyLoad>>();

        // Act
        var observer = new ServerObserver<DummyLoad>(localMockServer.Object);

        // Assert
        localMockServer.VerifyAdd(s => s.StateChanged += It.IsAny<Action<double>>(), Times.Once,
            "The observer should subscribe to the StateChanged event.");

        localMockServer.VerifyAdd(s => s.LoadDeparted += It.IsAny<Action<DummyLoad, double>>(), Times.Once,
            "The observer should subscribe to the LoadDeparted event.");
    }

    [Fact]
    [DisplayName("Observer should update its busy units metric when the server's state changes.")]
    public void OnServerStateChange_WhenEventFires_UpdatesBusyUnitsMetric()
    {
        // Arrange
        const int currentNumberInService = 3;
        const double eventTime = 50.0;

        _mockServer.SetupGet(s => s.NumberInService).Returns(currentNumberInService);

        // Act
        _mockServer.Raise(s => s.StateChanged += null, eventTime);

        // Assert
        Assert.Equal(currentNumberInService, _observer.BusyUnitsMetric.CurrentCount);
        Assert.Equal(eventTime, _observer.BusyUnitsMetric.CurrentTime);
    }

    [Fact]
    [DisplayName("Observer should increment loads completed count when a load departs.")]
    public void OnLoadDepart_WhenEventFires_IncrementsLoadsCompleted()
    {
        // Arrange
        Assert.Equal(0, _observer.LoadsCompleted); // Verify initial state

        // Act
        _mockServer.Raise(s => s.LoadDeparted += null, _testLoad, 10.0);
        _mockServer.Raise(s => s.LoadDeparted += null, _testLoad, 20.0);

        // Assert
        Assert.Equal(2, _observer.LoadsCompleted);
    }

    [Fact]
    [DisplayName("WarmedUp should reset statistics and re-observe the current server state.")]
    public void WarmedUp_WhenCalled_ResetsStatsAndReinitializes()
    {
        // Arrange
        _mockServer.Raise(s => s.LoadDeparted += null, _testLoad, 10.0);
        Assert.Equal(1, _observer.LoadsCompleted); // Verify pre-warmup state

        const int numberInServiceAtWarmup = 2;
        const double warmupTime = 100.0;

        _mockServer.SetupGet(s => s.NumberInService).Returns(numberInServiceAtWarmup);

        // Act
        _observer.WarmedUp(warmupTime);

        // Assert
        Assert.Equal(0, _observer.LoadsCompleted); // Loads completed should be reset.
        Assert.Equal(warmupTime, _observer.BusyUnitsMetric.InitialTime);
        Assert.Equal(warmupTime, _observer.BusyUnitsMetric.CurrentTime);
        Assert.Equal(numberInServiceAtWarmup, _observer.BusyUnitsMetric.CurrentCount);
    }

    [Fact]
    [DisplayName("Utilization property should calculate correctly based on metric average and server capacity.")]
    public void Utilization_WithPositiveCapacity_CalculatesCorrectly()
    {
        // Arrange
        _mockServer.SetupGet(s => s.Capacity).Returns(4); // Server has a capacity of 4.

        // The observer's metric is updated via the StateChanged event.
        // We simulate a scenario: 
        // - From T=0 to T=10, 0 units are busy.
        // - From T=10 to T=20, 2 units are busy.
        _mockServer.SetupGet(s => s.NumberInService).Returns(0);
        _mockServer.Raise(s => s.StateChanged += null, 0.0); // Initialize at T=0

        _mockServer.SetupGet(s => s.NumberInService).Returns(2);
        _mockServer.Raise(s => s.StateChanged += null, 10.0); // At time 10, count becomes 2.

        _mockServer.SetupGet(s => s.NumberInService).Returns(0);
        _mockServer.Raise(s => s.StateChanged += null, 20.0); // At time 20, count becomes 0.

        // Act
        // The calculation happens inside the property, so we just read it.
        double utilization = _observer.Utilization;

        // Assert
        // The TimeBasedMetric average count is: ((0 * 10) + (2 * 10)) / 20 = 20 / 20 = 1.0
        // Utilization should be AverageCount / Capacity = 1.0 / 4 = 0.25
        Assert.Equal(1.0, _observer.BusyUnitsMetric.AverageCount);
        Assert.Equal(0.25, utilization);
    }

    [Fact]
    [DisplayName("Utilization should be zero if server capacity is zero to avoid division by zero.")]
    public void Utilization_WithZeroCapacity_ReturnsZero()
    {
        // Arrange
        _mockServer.SetupGet(s => s.Capacity).Returns(0); // Edge case: capacity is 0.

        // Act
        double utilization = _observer.Utilization;

        // Assert
        Assert.Equal(0.0, utilization);
    }
}