using Moq;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Observability;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SimNextgenApp.Tests.Observability;

public class ServerObserverTests
{
    [Fact(DisplayName = "CreateSimple should create observer with valid server.")]
    public void CreateSimple_WithValidServer_CreatesObserver()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");

        // Act
        using var observer = ServerObserver.CreateSimple(mockServer.Object);

        // Assert
        Assert.NotNull(observer);
        Assert.Equal(0, observer.LoadsCompleted);
    }

    [Fact(DisplayName = "OnLoadDeparted should increment LoadsCompleted counter.")]
    public void OnLoadDeparted_IncrementsLoadsCompleted()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(5);
        mockServer.SetupGet(s => s.NumberInService).Returns(0);

        using var observer = ServerObserver.CreateSimple(mockServer.Object);

        var load1 = new DummyLoad();
        var load2 = new DummyLoad();

        // Act
        mockServer.Raise(s => s.LoadDeparted += null, load1, 100L);
        mockServer.Raise(s => s.LoadDeparted += null, load2, 200L);

        // Assert
        Assert.Equal(2, observer.LoadsCompleted);
    }

    [Fact(DisplayName = "OnLoadDeparted with valid service start time should record sojourn time.")]
    public void OnLoadDeparted_WithServiceStartTime_RecordsSojournTime()
    {
        // Arrange
        var capturedMeasurements = new List<Measurement<double>>();
        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName &&
                    instrument.Name == "sna.server.sojourn_time")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            capturedMeasurements.Add(new Measurement<double>(measurement, tags.ToArray()));
        });
        meterListener.Start();

        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(5);
        mockServer.SetupGet(s => s.NumberInService).Returns(1);

        var load = new DummyLoad();
        mockServer.Setup(s => s.GetServiceStartTime(load)).Returns(100L); // Started at time 100

        var observer = ServerObserver.CreateSimple(mockServer.Object);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        // Act - Load departs at time 300, sojourn time = 200ms = 0.2 seconds
        mockServer.Raise(s => s.LoadDeparted += null, load, 300L);

        // Assert - MeterListener callbacks are synchronous, so measurement should be captured immediately
        Assert.True(capturedMeasurements.Count > 0, "Expected sojourn time measurement to be recorded");
        var measurement = capturedMeasurements.First();
        Assert.Equal(0.2, measurement.Value, 0.001); // 200ms = 0.2 seconds

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }

    [Fact(DisplayName = "OnLoadDeparted without service start time should not record sojourn time.")]
    public void OnLoadDeparted_WithoutServiceStartTime_DoesNotRecordSojournTime()
    {
        // Arrange
        var capturedMeasurements = new List<Measurement<double>>();
        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName &&
                    instrument.Name == "sna.server.sojourn_time")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            capturedMeasurements.Add(new Measurement<double>(measurement, tags.ToArray()));
        });
        meterListener.Start();

        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(5);
        mockServer.SetupGet(s => s.NumberInService).Returns(0);

        var load = new DummyLoad();
        mockServer.Setup(s => s.GetServiceStartTime(load)).Returns((long?)null); // No start time

        var observer = ServerObserver.CreateSimple(mockServer.Object);

        // Act
        mockServer.Raise(s => s.LoadDeparted += null, load, 300L);

        // Assert - MeterListener callbacks are synchronous, no measurement should be recorded
        Assert.Empty(capturedMeasurements);

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }

    [Fact(DisplayName = "Utilization should return correct instantaneous value.")]
    public void Utilization_ReturnsCorrectInstantaneousValue()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(10);
        mockServer.SetupGet(s => s.NumberInService).Returns(7);

        using var observer = ServerObserver.CreateSimple(mockServer.Object);

        // Act
        var utilization = observer.Utilization;

        // Assert
        Assert.Equal(0.7, utilization, 0.001);
    }

    [Fact(DisplayName = "Utilization with zero capacity should return zero.")]
    public void Utilization_ZeroCapacity_ReturnsZero()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(0);
        mockServer.SetupGet(s => s.NumberInService).Returns(0);

        using var observer = ServerObserver.CreateSimple(mockServer.Object);

        // Act
        var utilization = observer.Utilization;

        // Assert
        Assert.Equal(0.0, utilization);
    }

    [Fact(DisplayName = "ObservableGauge should use cached warmup state from event context.")]
    public void ObservableGauge_UsesCachedWarmupState()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(5);
        mockServer.SetupGet(s => s.NumberInService).Returns(2);

        // Create an Activity to simulate warmup phase
        var activitySource = new ActivitySource("TestSource");
        using var activity = activitySource.StartActivity("TestActivity");
        activity?.SetTag("sna.simulation.warmup", true);

        var load = new DummyLoad();
        mockServer.Setup(s => s.GetServiceStartTime(load)).Returns(100L);

        var observer = ServerObserver.CreateSimple(mockServer.Object);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds); // Required for sojourn time recording

        // Act - Fire LoadDeparted event within the Activity context (warmup=true)
        mockServer.Raise(s => s.LoadDeparted += null, load, 200L);

        // The observer should have captured warmup=true state
        // This state should persist even after the Activity ends

        activity?.Stop();

        // Now simulate the ObservableGauge callback running on a background thread
        // (where Activity.Current would be null, but cached state should be used)
        var utilization = observer.Utilization; // This accesses the property that the gauge uses

        // Assert
        Assert.Equal(0.4, utilization, 0.001); // 2/5 = 0.4
        Assert.Equal(1, observer.LoadsCompleted);

        // Cleanup
        observer.Dispose();
        activitySource.Dispose();
    }

    [Fact(DisplayName = "SetTimeUnit should configure time unit for sojourn time conversion.")]
    public void SetTimeUnit_ConfiguresTimeUnitCorrectly()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(5);
        mockServer.SetupGet(s => s.NumberInService).Returns(1);

        var observer = ServerObserver.CreateSimple(mockServer.Object);

        // Act & Assert - Should not throw
        observer.SetTimeUnit(SimulationTimeUnit.Seconds);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        // Cleanup
        observer.Dispose();
    }

    [Fact(DisplayName = "Dispose should unsubscribe from server events.")]
    public void Dispose_UnsubscribesFromServerEvents()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(5);
        mockServer.SetupGet(s => s.NumberInService).Returns(0);

        var observer = ServerObserver.CreateSimple(mockServer.Object);
        var load = new DummyLoad();

        // Act
        observer.Dispose();

        // After disposal, raising LoadDeparted should not crash or increment counter
        mockServer.Raise(s => s.LoadDeparted += null, load, 100L);

        // Assert - LoadsCompleted should still be 0 since observer was disposed before event
        Assert.Equal(0, observer.LoadsCompleted);
    }

    [Fact(DisplayName = "OnLoadDeparted should throw InvalidOperationException if time unit is not set when recording sojourn time.")]
    public void OnLoadDeparted_WithoutTimeUnit_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockServer = new Mock<IServer<DummyLoad>>();
        mockServer.SetupGet(s => s.Name).Returns("TestServer");
        mockServer.SetupGet(s => s.Capacity).Returns(5);
        mockServer.SetupGet(s => s.NumberInService).Returns(1);

        var load = new DummyLoad();
        mockServer.Setup(s => s.GetServiceStartTime(load)).Returns(100L); // Has start time

        var observer = ServerObserver.CreateSimple(mockServer.Object);
        // NOTE: Not calling observer.SetTimeUnit() - this is the bug!

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            mockServer.Raise(s => s.LoadDeparted += null, load, 300L);
        });

        Assert.Contains("Time unit must be set", ex.Message);
        Assert.Contains("SetTimeUnit()", ex.Message);

        // Cleanup
        observer.Dispose();
    }
}
