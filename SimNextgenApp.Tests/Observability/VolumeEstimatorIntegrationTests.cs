using SimNextgenApp.Observability;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Tests.Observability;

/// <summary>
/// Integration tests verifying VolumeEstimator is properly wired through SimulationTelemetry.
/// </summary>
public class VolumeEstimatorIntegrationTests
{
    [Fact(DisplayName = "WithVolumeEstimation should create and expose VolumeEstimator.")]
    public void WithVolumeEstimation_CreatesAndExposesVolumeEstimator()
    {
        // Act
        var telemetry = SimulationTelemetry.Create()
            .WithVolumeEstimation(VolumeThresholds.Conservative())
            .Build();

        // Assert
        Assert.NotNull(telemetry.VolumeEstimator);
        Assert.Equal(0, telemetry.VolumeEstimator.TotalSpans);
        Assert.Equal(0, telemetry.VolumeEstimator.TotalMetricDataPoints);
    }

    [Fact(DisplayName = "Without WithVolumeEstimation, VolumeEstimator should be null.")]
    public void WithoutVolumeEstimation_VolumeEstimatorIsNull()
    {
        // Act
        var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .Build();

        // Assert
        Assert.Null(telemetry.VolumeEstimator);
        Assert.Equal(0, telemetry.SpansPerSecond);
        Assert.Equal(0, telemetry.MetricDataPointsPerSecond);
    }

    [Fact(DisplayName = "SpansPerSecond should return 0 when VolumeEstimator is not enabled.")]
    public void SpansPerSecond_ReturnsZero_WhenVolumeEstimatorNotEnabled()
    {
        // Arrange
        var telemetry = SimulationTelemetry.Create().Build();

        // Act
        var rate = telemetry.SpansPerSecond;

        // Assert
        Assert.Equal(0, rate);
    }
}
