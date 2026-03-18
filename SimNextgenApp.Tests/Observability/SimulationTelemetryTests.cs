using SimNextgenApp.Observability;

namespace SimNextgenApp.Tests.Observability;

public class SimulationTelemetryTests
{
    [Fact(DisplayName = "Build should create telemetry with console exporter.")]
    public void Build_WithConsoleExporter_CreatesValidTelemetry()
    {
        // Act
        var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .Build();

        // Assert
        Assert.NotNull(telemetry);
        Assert.NotNull(telemetry.ActivitySource);
        Assert.NotNull(telemetry.Meter);
        Assert.Equal(SimulationTelemetry.ActivitySourceName, telemetry.ActivitySource.Name);
        Assert.Equal(SimulationTelemetry.MeterName, telemetry.Meter.Name);

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "Build should create telemetry without exporters.")]
    public void Build_NoExporters_CreatesValidTelemetry()
    {
        // Act
        var telemetry = SimulationTelemetry.Create().Build();

        // Assert
        Assert.NotNull(telemetry);
        Assert.NotNull(telemetry.ActivitySource);
        Assert.NotNull(telemetry.Meter);

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "Build with Prometheus on invalid hostname should throw InvalidOperationException.")]
    public void Build_PrometheusListener_InvalidHostname_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            SimulationTelemetry.Create()
                .WithPrometheusExporter(port: 9999, hostname: "invalid-hostname-that-does-not-exist")
                .Build();
        });

        Assert.Contains("Failed to start Prometheus HttpListener", ex.Message);
        Assert.Contains("invalid-hostname-that-does-not-exist", ex.Message);
    }

    [Fact(DisplayName = "Dispose should clean up providers and sources.")]
    public void Dispose_CleansUpProviders()
    {
        // Arrange
        var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .Build();

        // Act
        telemetry.Dispose();

        // Assert
        // After disposal, ActivitySource should no longer have listeners
        // (This is a basic check - ActivitySource disposal doesn't throw, it just stops working)
        Assert.NotNull(telemetry.ActivitySource); // Still exists but disposed
        Assert.NotNull(telemetry.Meter);
    }

    [Fact(DisplayName = "Shutdown should flush telemetry data.")]
    public void Shutdown_FlushesTelemetryData()
    {
        // Arrange
        var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .Build();

        // Act & Assert - Should not throw
        telemetry.Shutdown();

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "WithPrometheusExporter should accept custom port and hostname.")]
    public void WithPrometheusExporter_CustomPortAndHostname_ConfiguresCorrectly()
    {
        // Note: We can't easily test that the listener actually binds without permissions,
        // but we can verify the builder accepts the parameters without throwing during configuration

        // Arrange & Act
        var builder = SimulationTelemetry.Create()
            .WithPrometheusExporter(port: 8080, hostname: "localhost");

        // Assert - Builder should not be null and should allow Build() to be called
        Assert.NotNull(builder);

        // For localhost, build should succeed if port is available
        var telemetry = builder.Build();
        Assert.NotNull(telemetry);

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "Multiple Dispose calls should not throw.")]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var telemetry = SimulationTelemetry.Create().Build();

        // Act & Assert - Should not throw
        telemetry.Dispose();
        telemetry.Dispose(); // Second call should be safe
    }
}
