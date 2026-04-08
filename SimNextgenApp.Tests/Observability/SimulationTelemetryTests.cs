using SimNextgenApp.Observability;
using SimNextgenApp.Observability.Exporters;

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
        Assert.Contains("does not form a valid URI for Prometheus", ex.Message);
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

    [Fact(DisplayName = "Flush should flush telemetry data and return success.")]
    public void Flush_FlushesTelemetryData()
    {
        // Arrange
        var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .Build();

        // Act
        bool flushSuccess = telemetry.Flush();

        // Assert
        Assert.True(flushSuccess, "Flush should return true indicating all providers flushed successfully");

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "Flush should reject zero or negative timeout.")]
    public void Flush_ZeroOrNegativeTimeout_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var telemetry = SimulationTelemetry.Create().Build();

        // Act & Assert - Zero timeout
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => telemetry.Flush(0));
        Assert.Equal("timeoutMilliseconds", ex1.ParamName);

        // Negative timeout
        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => telemetry.Flush(-100));
        Assert.Equal("timeoutMilliseconds", ex2.ParamName);
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

    [Fact(DisplayName = "Build should throw when both OTLP exporter configurations are used.")]
    public void Build_WithBothOtlpExporterConfigurations_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = SimulationTelemetry.Create()
            .WithOtlpExporter("http://localhost:4317")
            .WithOtlpExporter(OtlpBackend.Datadog, "test-api-key");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("mutually exclusive", ex.Message);
        Assert.Contains("WithOtlpExporter", ex.Message);
    }

    [Fact(DisplayName = "Build should throw when both OTLP exporter configurations are used in reverse order.")]
    public void Build_WithBothOtlpExporterConfigurationsReversed_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = SimulationTelemetry.Create()
            .WithOtlpExporter(OtlpBackend.GrafanaCloud, "test-api-key")
            .WithOtlpExporter("http://localhost:4317");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact(DisplayName = "Build should succeed with single OTLP endpoint configuration.")]
    public void Build_WithSingleOtlpEndpointConfiguration_Succeeds()
    {
        // Arrange & Act
        var telemetry = SimulationTelemetry.Create()
            .WithOtlpExporter("http://localhost:4317")
            .Build();

        // Assert
        Assert.NotNull(telemetry);

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "Build should succeed with single OTLP backend configuration.")]
    public void Build_WithSingleOtlpBackendConfiguration_Succeeds()
    {
        // Arrange & Act
        var telemetry = SimulationTelemetry.Create()
            .WithOtlpExporter(OtlpBackend.Honeycomb, "test-api-key")
            .Build();

        // Assert
        Assert.NotNull(telemetry);

        // Cleanup
        telemetry.Dispose();
    }

    [Fact(DisplayName = "WithOtlpExporter should reject null or empty endpoint.")]
    public void WithOtlpExporter_NullOrEmptyEndpoint_ThrowsArgumentException()
    {
        // Act & Assert - Null endpoint
        var ex1 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create().WithOtlpExporter((string)null!);
        });
        Assert.Equal("endpoint", ex1.ParamName);

        // Empty endpoint
        var ex2 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create().WithOtlpExporter("");
        });
        Assert.Equal("endpoint", ex2.ParamName);

        // Whitespace endpoint
        var ex3 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create().WithOtlpExporter("   ");
        });
        Assert.Equal("endpoint", ex3.ParamName);
    }

    [Fact(DisplayName = "WithOtlpExporter should reject invalid endpoint URI.")]
    public void WithOtlpExporter_InvalidEndpointUri_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create().WithOtlpExporter("not-a-valid-uri");
        });
        Assert.Equal("endpoint", ex.ParamName);
        Assert.Contains("not a valid absolute URI", ex.Message);
    }

    [Fact(DisplayName = "WithOtlpExporter backend overload should reject null or empty API key.")]
    public void WithOtlpExporter_BackendOverload_NullOrEmptyApiKey_ThrowsArgumentException()
    {
        // Act & Assert - Null API key
        var ex1 = Assert.Throws<ArgumentNullException>(() =>
        {
            SimulationTelemetry.Create().WithOtlpExporter(OtlpBackend.Datadog, null!);
        });
        Assert.Equal("apiKey", ex1.ParamName);

        // Empty API key
        var ex2 = Assert.Throws<ArgumentNullException>(() =>
        {
            SimulationTelemetry.Create().WithOtlpExporter(OtlpBackend.Datadog, "");
        });
        Assert.Equal("apiKey", ex2.ParamName);

        // Whitespace API key
        var ex3 = Assert.Throws<ArgumentNullException>(() =>
        {
            SimulationTelemetry.Create().WithOtlpExporter(OtlpBackend.Datadog, "   ");
        });
        Assert.Equal("apiKey", ex3.ParamName);
    }

    [Fact(DisplayName = "WithServiceInfo should reject null or empty service name.")]
    public void WithServiceInfo_NullOrEmptyServiceName_ThrowsArgumentException()
    {
        // Act & Assert - Null service name
        var ex1 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create().WithServiceInfo(null!);
        });
        Assert.Equal("serviceName", ex1.ParamName);

        // Empty service name
        var ex2 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create().WithServiceInfo("");
        });
        Assert.Equal("serviceName", ex2.ParamName);

        // Whitespace service name
        var ex3 = Assert.Throws<ArgumentException>(() =>
        {
            SimulationTelemetry.Create().WithServiceInfo("   ");
        });
        Assert.Equal("serviceName", ex3.ParamName);
    }

    [Fact(DisplayName = "WithServiceInfo should accept valid inputs and return builder.")]
    public void WithServiceInfo_ValidInputs_DoesNotThrow()
    {
        // Act
        var builder = SimulationTelemetry.Create()
            .WithServiceInfo("MyCustomService", "2.5.0");

        // Assert
        Assert.NotNull(builder);
        
        using var telemetry = builder.Build();
        Assert.NotNull(telemetry);
    }
}
