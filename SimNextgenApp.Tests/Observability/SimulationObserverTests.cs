using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Modeling;
using SimNextgenApp.Observability;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SimNextgenApp.Tests.Observability;

public class SimulationObserverTests
{
    [Fact(DisplayName = "CreateSimple should create observer with valid engine.")]
    public void CreateSimple_WithValidEngine_CreatesObserver()
    {
        // Arrange
        var mockModel = new Mock<ISimulationModel>();
        mockModel.SetupGet(m => m.Name).Returns("TestModel");
        mockModel.SetupGet(m => m.Id).Returns(1L);

        var profile = new SimulationProfile(
            model: mockModel.Object,
            runStrategy: new AbsoluteTimeRunStrategy(stopTime: 1000));

        var engine = new SimulationEngine(profile);

        // Act
        using var observer = SimulationObserver.CreateSimple(engine);

        // Assert
        Assert.NotNull(observer);
        Assert.Equal(0, observer.TotalEventsExecuted);
        Assert.Equal(0, observer.SimulationClockTime);
    }

    [Fact(DisplayName = "Observer should track events per second correctly.")]
    public void Observer_TracksEventsPerSecond()
    {
        // Arrange
        var mockModel = new Mock<ISimulationModel>();
        mockModel.SetupGet(m => m.Name).Returns("TestModel");
        mockModel.SetupGet(m => m.Id).Returns(1L);

        var profile = new SimulationProfile(
            model: mockModel.Object,
            runStrategy: new AbsoluteTimeRunStrategy(stopTime: 1000));

        var engine = new SimulationEngine(profile);

        using var observer = SimulationObserver.CreateSimple(engine);

        // Act
        Thread.Sleep(100); // Let some real time pass

        // Assert
        Assert.True(observer.EventsPerSecond >= 0);
    }

    [Fact(DisplayName = "Observer should track elapsed real time.")]
    public void Observer_TracksElapsedRealTime()
    {
        // Arrange
        var mockModel = new Mock<ISimulationModel>();
        mockModel.SetupGet(m => m.Name).Returns("TestModel");
        mockModel.SetupGet(m => m.Id).Returns(1L);

        var profile = new SimulationProfile(
            model: mockModel.Object,
            runStrategy: new AbsoluteTimeRunStrategy(stopTime: 1000));

        var engine = new SimulationEngine(profile);

        using var observer = SimulationObserver.CreateSimple(engine);

        // Act
        Thread.Sleep(100);

        // Assert
        Assert.True(observer.ElapsedRealTime > 0);
        Assert.True(observer.ElapsedRealTime >= 0.1); // At least 100ms
    }

    [Fact(DisplayName = "WarmupComplete should return true when no warmup configured.")]
    public void WarmupComplete_NoWarmup_ReturnsTrue()
    {
        // Arrange
        var mockModel = new Mock<ISimulationModel>();
        mockModel.SetupGet(m => m.Name).Returns("TestModel");
        mockModel.SetupGet(m => m.Id).Returns(1L);

        var profile = new SimulationProfile(
            model: mockModel.Object,
            runStrategy: new AbsoluteTimeRunStrategy(stopTime: 1000));

        var engine = new SimulationEngine(profile);

        using var observer = SimulationObserver.CreateSimple(engine);

        // Assert
        Assert.True(observer.WarmupComplete);
    }

    [Fact(DisplayName = "RecordEventExecution should emit event counter metric.")]
    public void RecordEventExecution_EmitsEventCounterMetric()
    {
        // Arrange
        var capturedMeasurements = new List<Measurement<long>>();
        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName &&
                    instrument.Name == "sna.simulation.events_total")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            capturedMeasurements.Add(new Measurement<long>(measurement, tags.ToArray()));
        });
        meterListener.Start();

        var mockModel = new Mock<ISimulationModel>();
        mockModel.SetupGet(m => m.Name).Returns("TestModel");
        mockModel.SetupGet(m => m.Id).Returns(1L);

        var profile = new SimulationProfile(
            model: mockModel.Object,
            runStrategy: new AbsoluteTimeRunStrategy(stopTime: 1000));

        var engine = new SimulationEngine(profile);
        var observer = SimulationObserver.CreateSimple(engine);

        // Act
        observer.RecordEventExecution();
        observer.RecordEventExecution();

        // Assert
        Assert.Equal(2, capturedMeasurements.Count);
        Assert.All(capturedMeasurements, m => Assert.Equal(1L, m.Value));

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }

    [Fact(DisplayName = "Observable gauges should emit simulation metrics.")]
    public void ObservableGauges_EmitSimulationMetrics()
    {
        // Arrange
        var capturedClockTime = new List<Measurement<long>>();
        var capturedRealTime = new List<Measurement<double>>();
        var capturedEventsPerSecond = new List<Measurement<double>>();

        var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SimulationTelemetry.MeterName)
                {
                    if (instrument.Name == "sna.simulation.clock_time" ||
                        instrument.Name == "sna.simulation.real_time_elapsed" ||
                        instrument.Name == "sna.simulation.events_per_second")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sna.simulation.clock_time")
            {
                capturedClockTime.Add(new Measurement<long>(measurement, tags.ToArray()));
            }
        });

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sna.simulation.real_time_elapsed")
            {
                capturedRealTime.Add(new Measurement<double>(measurement, tags.ToArray()));
            }
            else if (instrument.Name == "sna.simulation.events_per_second")
            {
                capturedEventsPerSecond.Add(new Measurement<double>(measurement, tags.ToArray()));
            }
        });

        meterListener.Start();

        var mockModel = new Mock<ISimulationModel>();
        mockModel.SetupGet(m => m.Name).Returns("TestModel");
        mockModel.SetupGet(m => m.Id).Returns(1L);

        var profile = new SimulationProfile(
            model: mockModel.Object,
            runStrategy: new AbsoluteTimeRunStrategy(stopTime: 1000));

        var engine = new SimulationEngine(profile);
        var observer = SimulationObserver.CreateSimple(engine);

        // Act - Trigger observable gauge collection
        meterListener.RecordObservableInstruments();

        // Assert
        Assert.NotEmpty(capturedClockTime);
        Assert.NotEmpty(capturedRealTime);
        Assert.NotEmpty(capturedEventsPerSecond);

        // Cleanup
        meterListener.Dispose();
        observer.Dispose();
    }
}
