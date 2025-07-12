using Moq;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Tests.Statistics;

public class ServerObserverTests
{
    private Server<DummyLoad> CreateTestServer(int capacity = 1)
    {
        var config = new ServerStaticConfig<DummyLoad>((load, random) => TimeSpan.FromSeconds(10))
        {
            Capacity = capacity
        };
        var server = new Server<DummyLoad>(config, seed: 123, instanceName: "TestServer");

        return server;
    }

    //[Fact]
    //public void OnLoadDepart_IncrementsLoadsCompletedCount()
    //{
    //    // Arrange
    //    var server = CreateTestServer();
    //    var observer = new ServerObserver<DummyLoad>(server);
    //    var load = new DummyLoad("L1");

    //    Assert.Equal(0, observer.LoadsCompleted); // Pre-condition

    //    // Act
    //    // Manually trigger the server's completion logic, which fires the event hook.
    //    // We need to add the load first so Remove succeeds.
    //    server.LoadsInService.Add(load);
    //    server.HandleServiceCompletion(load, currentTime: 10.0);

    //    // Assert
    //    Assert.Equal(1, observer.LoadsCompleted);
    //}

    //[Fact]
    //public void OnServerStateChange_UpdatesBusyUnitsMetric_OnArrival()
    //{
    //    // Arrange
    //    var server = CreateTestServer(capacity: 2);
    //    var observer = new ServerObserver<DummyLoad>(server);
    //    var load1 = new DummyLoad("L1");

    //    Assert.Equal(0, observer.BusyUnitsMetric.CurrentCount); // Pre-condition

    //    // Act
    //    // Manually trigger the server's arrival logic.
    //    server.HandleLoadArrivalForService(load1, currentTime: 10.0);

    //    // Assert
    //    Assert.Equal(1, observer.BusyUnitsMetric.CurrentCount);
    //}

    //[Fact]
    //public void OnServerStateChange_UpdatesBusyUnitsMetric_OnDeparture()
    //{
    //    // Arrange
    //    var server = CreateTestServer(capacity: 2);
    //    var observer = new ServerObserver<DummyLoad>(server);
    //    var load1 = new DummyLoad("L1");

    //    // Simulate a state where the server is busy
    //    server.HandleLoadArrivalForService(load1, currentTime: 10.0);
    //    Assert.Equal(1, observer.BusyUnitsMetric.CurrentCount); // Pre-condition

    //    // Act
    //    // Now, trigger the departure logic.
    //    server.HandleServiceCompletion(load1, currentTime: 20.0);

    //    // Assert
    //    Assert.Equal(0, observer.BusyUnitsMetric.CurrentCount);
    //}

    //[Fact]
    //public void Utilization_Property_CalculatesCorrectly()
    //{
    //    // Arrange
    //    var server = CreateTestServer(capacity: 2);
    //    var observer = new ServerObserver<DummyLoad>(server);
    //    var load1 = new DummyLoad("L1");
    //    var load2 = new DummyLoad("L2");

    //    // Act & Assert
    //    // At T=10, the server becomes busy with 1 load.
    //    server.HandleLoadArrivalForService(load1, 10.0);    // T=10, busy=1
    //                                                        // T=10 to 20: 1 busy.

    //    observer.BusyUnitsMetric.ObserveCount(server.NumberInService, 20.0);

    //    server.HandleLoadArrivalForService(load2, 20.0);    // T=20, busy=2
    //                                                        // T=20 to 30: 2 busy.
    //    server.HandleServiceCompletion(load1, 30.0);        // T=30, busy=1
    //                                                        // T=30 to 40: 1 busy.
    //    observer.BusyUnitsMetric.ObserveCount(server.NumberInService, 40.0); // Final observation at T=40

    //    // Calculation:
    //    // (0 * 10) + (1 * 10) + (2 * 10) + (1 * 10) = 0 + 10 + 20 + 10 = 40 (CumulativeCountTimeProduct)
    //    // Total duration = 40
    //    // AverageCount = 40 / 40 = 1.0
    //    // Utilization = AverageCount / Capacity = 1.0 / 2.0 = 0.5

    //    Assert.Equal(0.5, observer.Utilization, 5); // Use precision for double comparison
    //}

    //[Fact]
    //public void WarmedUp_ResetsAllStatistics_AndReinitializesCount()
    //{
    //    // Arrange
    //    var server = CreateTestServer(capacity: 5);
    //    var observer = new ServerObserver<DummyLoad>(server);

    //    // Simulate a "dirty" state with pre-warm-up data
    //    var load1 = new DummyLoad("L1");
    //    var load2 = new DummyLoad("L2");
    //    var load3 = new DummyLoad("L3");
    //    var load4 = new DummyLoad("L4");
    //    server.HandleLoadArrivalForService(load1, 10.0);
    //    server.HandleLoadArrivalForService(load2, 20.0);
    //    server.HandleServiceCompletion(load1, 30.0);

    //    // Sanity check the dirty state
    //    Assert.Equal(1, observer.LoadsCompleted);
    //    Assert.Equal(1, observer.BusyUnitsMetric.CurrentCount);

    //    // Now, put the server in its final pre-warm-up state.
    //    // It should have 3 busy units (L2, L3, L4)
    //    server.HandleLoadArrivalForService(load3, 98.0);
    //    server.HandleLoadArrivalForService(load4, 99.0);
    //    Assert.Equal(3, server.NumberInService);

    //    double warmUpTime = 100.0;

    //    // Act
    //    observer.WarmedUp(warmUpTime);

    //    // Assert
    //    // 1. Verify statistics are cleared
    //    Assert.Equal(0, observer.LoadsCompleted);
    //    Assert.Equal(0, observer.BusyUnitsMetric.TotalActiveDuration);

    //    // 2. Verify BusyUnitsMetric is re-initialized with the server's current state
    //    Assert.Equal(warmUpTime, observer.BusyUnitsMetric.InitialTime);
    //    Assert.Equal(3, observer.BusyUnitsMetric.CurrentCount);
    //}
}
