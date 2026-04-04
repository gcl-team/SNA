using Moq;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Observability;
using System.Diagnostics.Metrics;

namespace SimNextgenApp.Tests.Observability;

public class GeneratorObserverTests
{
    [Fact(DisplayName = "CreateSimple should create observer with valid generator.")]
    public void CreateSimple_WithValidGenerator_CreatesObserver()
    {
        // Arrange
        var mockGenerator = new Mock<IGenerator<DummyLoad>>();
        mockGenerator.SetupGet(g => g.Name).Returns("TestGenerator");

        // Act
        using var observer = GeneratorObserver.CreateSimple(mockGenerator.Object);

        // Assert
        Assert.NotNull(observer);
        Assert.Equal(0, observer.LoadsGenerated);
        Assert.Null(observer.LastInterArrivalTime);
    }

    [Fact(DisplayName = "OnLoadGenerated should increment LoadsGenerated counter.")]
    public void OnLoadGenerated_IncrementsLoadsGenerated()
    {
        // Arrange
        var mockGenerator = new Mock<IGenerator<DummyLoad>>();
        mockGenerator.SetupGet(g => g.Name).Returns("TestGenerator");

        using var observer = GeneratorObserver.CreateSimple(mockGenerator.Object);
        observer.SetTimeUnit(SimulationTimeUnit.Seconds);

        var load1 = new DummyLoad();
        var load2 = new DummyLoad();

        // Act
        mockGenerator.Raise(g => g.LoadGenerated += null, load1, 100L);
        mockGenerator.Raise(g => g.LoadGenerated += null, load2, 250L);

        // Assert
        Assert.Equal(2, observer.LoadsGenerated);
    }

    [Fact(DisplayName = "OnLoadGenerated should track inter-arrival time after second load.")]
    public void OnLoadGenerated_TracksInterArrivalTime()
    {
        // Arrange
        var mockGenerator = new Mock<IGenerator<DummyLoad>>();
        mockGenerator.SetupGet(g => g.Name).Returns("TestGenerator");

        using var observer = GeneratorObserver.CreateSimple(mockGenerator.Object);
        observer.SetTimeUnit(SimulationTimeUnit.Seconds);

        var load1 = new DummyLoad();
        var load2 = new DummyLoad();
        var load3 = new DummyLoad();

        // Act
        mockGenerator.Raise(g => g.LoadGenerated += null, load1, 100L);
        Assert.Null(observer.LastInterArrivalTime); // No inter-arrival time yet

        mockGenerator.Raise(g => g.LoadGenerated += null, load2, 250L);
        Assert.Equal(150L, observer.LastInterArrivalTime); // 250 - 100 = 150

        mockGenerator.Raise(g => g.LoadGenerated += null, load3, 320L);
        Assert.Equal(70L, observer.LastInterArrivalTime); // 320 - 250 = 70

        // Assert
        Assert.Equal(3, observer.LoadsGenerated);
    }

    [Fact(DisplayName = "OnLoadGenerated should record inter-arrival time histogram.")]
    public void OnLoadGenerated_RecordsInterArrivalTimeHistogram()
    {
        // Arrange
        var capturedMeasurements = new List<Measurement<double>>();
        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName &&
                    instrument.Name == "sna.generator.inter_arrival_time")
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

        var mockGenerator = new Mock<IGenerator<DummyLoad>>();
        mockGenerator.SetupGet(g => g.Name).Returns("TestGenerator");

        var observer = GeneratorObserver.CreateSimple(mockGenerator.Object);
        observer.SetTimeUnit(SimulationTimeUnit.Milliseconds);

        var load1 = new DummyLoad();
        var load2 = new DummyLoad();

        // Act - Inter-arrival time: 250 - 100 = 150ms = 0.15 seconds
        mockGenerator.Raise(g => g.LoadGenerated += null, load1, 100L);
        mockGenerator.Raise(g => g.LoadGenerated += null, load2, 250L);

        // Assert
        Assert.True(capturedMeasurements.Count > 0, "Expected inter-arrival time measurement to be recorded");
        var measurement = capturedMeasurements.First();
        Assert.Equal(0.15, measurement.Value, 0.001); // 150ms = 0.15 seconds

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }

    [Fact(DisplayName = "OnLoadGenerated should record loads generated counter.")]
    public void OnLoadGenerated_RecordsLoadsGeneratedCounter()
    {
        // Arrange
        var capturedMeasurements = new List<Measurement<int>>();
        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName &&
                    instrument.Name == "sna.generator.loads_generated")
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

        var mockGenerator = new Mock<IGenerator<DummyLoad>>();
        mockGenerator.SetupGet(g => g.Name).Returns("TestGenerator");

        var observer = GeneratorObserver.CreateSimple(mockGenerator.Object);
        observer.SetTimeUnit(SimulationTimeUnit.Seconds);

        var load1 = new DummyLoad();
        var load2 = new DummyLoad();

        // Act
        mockGenerator.Raise(g => g.LoadGenerated += null, load1, 100L);
        mockGenerator.Raise(g => g.LoadGenerated += null, load2, 250L);

        // Assert
        Assert.Equal(2, capturedMeasurements.Count);
        Assert.All(capturedMeasurements, m => Assert.Equal(1, m.Value)); // Each increment is 1

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }

    [Fact(DisplayName = "CreateSimple should throw ArgumentNullException for null generator.")]
    public void CreateSimple_WithNullGenerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => GeneratorObserver.CreateSimple<DummyLoad>(null!));
    }

    [Fact(DisplayName = "Dispose should unsubscribe from generator events.")]
    public void Dispose_UnsubscribesFromGeneratorEvents()
    {
        // Arrange
        var mockGenerator = new Mock<IGenerator<DummyLoad>>();
        mockGenerator.SetupGet(g => g.Name).Returns("TestGenerator");

        var observer = GeneratorObserver.CreateSimple(mockGenerator.Object);
        var load = new DummyLoad();

        // Act - Dispose observer
        observer.Dispose();

        // Raise event after disposal
        mockGenerator.Raise(g => g.LoadGenerated += null, load, 100L);

        // Assert - LoadsGenerated should not increment after disposal
        Assert.Equal(0, observer.LoadsGenerated);
    }

    [Fact(DisplayName = "OnLoadGenerated should throw InvalidOperationException if time unit not set.")]
    public void OnLoadGenerated_WithoutTimeUnit_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockGenerator = new Mock<IGenerator<DummyLoad>>();
        mockGenerator.SetupGet(g => g.Name).Returns("TestGenerator");

        using var observer = GeneratorObserver.CreateSimple(mockGenerator.Object);
        // Note: NOT calling SetTimeUnit()

        var load1 = new DummyLoad();
        var load2 = new DummyLoad();

        // Act - First load is fine (no inter-arrival time yet)
        mockGenerator.Raise(g => g.LoadGenerated += null, load1, 100L);

        // Act & Assert - Second load should throw because inter-arrival time needs conversion
        var exception = Assert.Throws<InvalidOperationException>(() =>
            mockGenerator.Raise(g => g.LoadGenerated += null, load2, 250L));

        Assert.Contains("Time unit must be set", exception.Message);
    }
}

public class DummyLoad
{
    public int Id { get; set; }
}
