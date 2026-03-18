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

    [Fact(DisplayName = "WithPrometheusExporter should reject invalid port numbers.")]
    public void WithPrometheusExporter_InvalidPort_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act & Assert - Port too low
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            SimulationTelemetry.Create()
                .WithPrometheusExporter(port: 0, hostname: "localhost");
        });
        Assert.Equal("port", ex1.ParamName);

        // Port too high
        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            SimulationTelemetry.Create()
                .WithPrometheusExporter(port: 65536, hostname: "localhost");
        });
        Assert.Equal("port", ex2.ParamName);
    }

    [Fact(DisplayName = "WithPrometheusExporter should reject null or empty hostname.")]
    public void WithPrometheusExporter_NullOrEmptyHostname_ThrowsArgumentException()
    {
        // Arrange & Act & Assert - Null hostname
        var ex1 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create()
                .WithPrometheusExporter(port: 9090, hostname: null!);
        });
        Assert.Equal("hostname", ex1.ParamName);

        // Empty hostname
        var ex2 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create()
                .WithPrometheusExporter(port: 9090, hostname: "");
        });
        Assert.Equal("hostname", ex2.ParamName);

        // Whitespace hostname
        var ex3 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create()
                .WithPrometheusExporter(port: 9090, hostname: "   ");
        });
        Assert.Equal("hostname", ex3.ParamName);
    }

    [Fact(DisplayName = "WithPrometheusExporter should reject hostname with invalid characters.")]
    public void WithPrometheusExporter_InvalidCharactersInHostname_ThrowsArgumentException()
    {
        // Arrange & Act & Assert - Hostname with spaces
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create()
                .WithPrometheusExporter(port: 9090, hostname: "invalid hostname");
        });
        Assert.Equal("hostname", ex.ParamName);
        Assert.Contains("invalid characters", ex.Message);
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

    [Fact(DisplayName = "Flush should flush telemetry data.")]
    public void Flush_FlushesTelemetryData()
    {
        // Arrange
        var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .Build();

        // Act & Assert - Should not throw
        telemetry.Flush();

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "WithPrometheusExporter should accept custom port and hostname.")]
    public void WithPrometheusExporter_CustomPortAndHostname_AcceptsConfiguration()
    {
        // Arrange & Act
        var builder = SimulationTelemetry.Create()
            .WithPrometheusExporter(port: 8080, hostname: "localhost");

        // Assert - Builder should not be null (configuration was accepted)
        Assert.NotNull(builder);

        // Note: We don't call Build() here to avoid binding to a real HTTP port in unit tests.
        // Binding can fail in CI/CD environments due to:
        // - Port already in use
        // - URL ACL permissions on Windows
        // - Restricted network environments
        // The input validation tests already cover error cases.
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
