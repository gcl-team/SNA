using Moq;
using SimNextgenApp.Modeling.Resource;
using SimNextgenApp.Observability;
using System.Diagnostics.Metrics;

namespace SimNextgenApp.Tests.Observability;

public class ResourceObserverTests
{
    [Fact(DisplayName = "CreateSimple should create observer with valid resource pool.")]
    public void CreateSimple_WithValidResourcePool_CreatesObserver()
    {
        // Arrange
        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(10);
        mockResourcePool.SetupGet(r => r.AvailableCount).Returns(10);
        mockResourcePool.SetupGet(r => r.BusyCount).Returns(0);

        // Act
        using var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        // Assert
        Assert.NotNull(observer);
        Assert.Equal(10, observer.Available);
        Assert.Equal(0, observer.InUse);
        Assert.Equal(0.0, observer.Utilization);
    }

    [Fact(DisplayName = "OnResourceAcquired should increment acquisitions counter.")]
    public void OnResourceAcquired_IncrementsAcquisitionsCount()
    {
        // Arrange
        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(5);
        mockResourcePool.SetupGet(r => r.AvailableCount).Returns(3);
        mockResourcePool.SetupGet(r => r.BusyCount).Returns(2);

        using var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        var resource1 = new DummyResource();
        var resource2 = new DummyResource();

        // Act
        mockResourcePool.Raise(r => r.ResourceAcquired += null, resource1, 100L);
        mockResourcePool.Raise(r => r.ResourceAcquired += null, resource2, 200L);

        // Assert
        Assert.Equal(2, observer.AcquisitionsCount);
    }

    [Fact(DisplayName = "OnResourceReleased should increment releases counter.")]
    public void OnResourceReleased_IncrementsReleasesCount()
    {
        // Arrange
        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(5);
        mockResourcePool.SetupGet(r => r.AvailableCount).Returns(4);
        mockResourcePool.SetupGet(r => r.BusyCount).Returns(1);

        using var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        var resource1 = new DummyResource();
        var resource2 = new DummyResource();

        // Act
        mockResourcePool.Raise(r => r.ResourceReleased += null, resource1, 100L);
        mockResourcePool.Raise(r => r.ResourceReleased += null, resource2, 200L);

        // Assert
        Assert.Equal(2, observer.ReleasesCount);
    }

    [Fact(DisplayName = "OnRequestFailed should increment failed requests counter.")]
    public void OnRequestFailed_IncrementsFailedRequestsCount()
    {
        // Arrange
        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(5);
        mockResourcePool.SetupGet(r => r.AvailableCount).Returns(0);
        mockResourcePool.SetupGet(r => r.BusyCount).Returns(5);

        using var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        // Act
        mockResourcePool.Raise(r => r.RequestFailed += null, 100L);
        mockResourcePool.Raise(r => r.RequestFailed += null, 200L);

        // Assert
        Assert.Equal(2, observer.FailedRequestsCount);
    }

    [Fact(DisplayName = "Utilization should calculate correctly.")]
    public void Utilization_CalculatesCorrectly()
    {
        // Arrange
        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(10);
        mockResourcePool.SetupGet(r => r.AvailableCount).Returns(3);
        mockResourcePool.SetupGet(r => r.BusyCount).Returns(7);

        using var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        // Act & Assert
        Assert.Equal(0.7, observer.Utilization, 0.001); // 7 / 10 = 0.7
    }

    [Fact(DisplayName = "Utilization should return zero when capacity is zero.")]
    public void Utilization_WithZeroCapacity_ReturnsZero()
    {
        // Arrange
        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(0);
        mockResourcePool.SetupGet(r => r.AvailableCount).Returns(0);
        mockResourcePool.SetupGet(r => r.BusyCount).Returns(0);

        using var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        // Act & Assert
        Assert.Equal(0.0, observer.Utilization);
    }

    [Fact(DisplayName = "OnResourceAcquired should record acquisitions counter metric.")]
    public void OnResourceAcquired_RecordsAcquisitionsCounter()
    {
        // Arrange
        var capturedMeasurements = new List<Measurement<int>>();
        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName &&
                    instrument.Name == "sna.resource.acquisitions")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            capturedMeasurements.Add(new Measurement<int>(measurement, tags.ToArray()));
        });
        meterListener.Start();

        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(5);

        var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        var resource = new DummyResource();

        // Act
        mockResourcePool.Raise(r => r.ResourceAcquired += null, resource, 100L);

        // Assert
        Assert.True(capturedMeasurements.Count > 0, "Expected acquisitions counter to be recorded");
        Assert.Equal(1, capturedMeasurements.First().Value);

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }

    [Fact(DisplayName = "Observer should record all gauge metrics.")]
    public void Observer_RecordsAllGaugeMetrics()
    {
        // Arrange
        var capturedAvailable = new List<Measurement<int>>();
        var capturedInUse = new List<Measurement<int>>();
        var capturedUtilization = new List<Measurement<double>>();

        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName)
                {
                    if (instrument.Name == "sna.resource.available" ||
                        instrument.Name == "sna.resource.in_use" ||
                        instrument.Name == "sna.resource.utilization")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            }
        };

        meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sna.resource.available")
                capturedAvailable.Add(new Measurement<int>(measurement, tags.ToArray()));
            else if (instrument.Name == "sna.resource.in_use")
                capturedInUse.Add(new Measurement<int>(measurement, tags.ToArray()));
        });

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sna.resource.utilization")
                capturedUtilization.Add(new Measurement<double>(measurement, tags.ToArray()));
        });

        meterListener.Start();

        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(10);
        mockResourcePool.SetupGet(r => r.AvailableCount).Returns(3);
        mockResourcePool.SetupGet(r => r.BusyCount).Returns(7);

        var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);

        // Act - Force a measurement by triggering RecordObservableInstruments
        meterListener.RecordObservableInstruments();

        // Assert
        Assert.True(capturedAvailable.Count > 0, "Expected available gauge to be recorded");
        Assert.Equal(3, capturedAvailable.First().Value);

        Assert.True(capturedInUse.Count > 0, "Expected in_use gauge to be recorded");
        Assert.Equal(7, capturedInUse.First().Value);

        Assert.True(capturedUtilization.Count > 0, "Expected utilization gauge to be recorded");
        Assert.Equal(0.7, capturedUtilization.First().Value, 0.001);

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }

    [Fact(DisplayName = "CreateSimple should throw ArgumentNullException for null resource pool.")]
    public void CreateSimple_WithNullResourcePool_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ResourceObserver.CreateSimple<DummyResource>(null!));
    }

    [Fact(DisplayName = "Dispose should unsubscribe from resource pool events.")]
    public void Dispose_UnsubscribesFromResourcePoolEvents()
    {
        // Arrange
        var mockResourcePool = new Mock<IResourcePool<DummyResource>>();
        mockResourcePool.SetupGet(r => r.Name).Returns("TestResourcePool");
        mockResourcePool.SetupGet(r => r.TotalCapacity).Returns(5);

        var observer = ResourceObserver.CreateSimple(mockResourcePool.Object);
        var resource = new DummyResource();

        // Act - Dispose observer
        observer.Dispose();

        // Raise events after disposal
        mockResourcePool.Raise(r => r.ResourceAcquired += null, resource, 100L);
        mockResourcePool.Raise(r => r.ResourceReleased += null, resource, 200L);
        mockResourcePool.Raise(r => r.RequestFailed += null, 300L);

        // Assert - Counters should not increment after disposal
        Assert.Equal(0, observer.AcquisitionsCount);
        Assert.Equal(0, observer.ReleasesCount);
        Assert.Equal(0, observer.FailedRequestsCount);
    }
}

public class DummyResource
{
    public int Id { get; set; }
}
