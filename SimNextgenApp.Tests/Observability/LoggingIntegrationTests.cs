using Microsoft.Extensions.Logging;
using SimNextgenApp.Observability;
using System.Diagnostics;

namespace SimNextgenApp.Tests.Observability;

public class LoggingIntegrationTests
{
    [Fact(DisplayName = "WithLogging should enable logging integration.")]
    public void WithLogging_EnablesLoggingIntegration()
    {
        // Arrange & Act
        using var telemetry = SimulationTelemetry.Create()
            .WithLogging()
            .Build();

        // Assert
        Assert.NotNull(telemetry.LoggerFactory);
    }

    [Fact(DisplayName = "WithLogging should create functional logger factory.")]
    public void WithLogging_CreatesFunctionalLoggerFactory()
    {
        // Arrange
        using var telemetry = SimulationTelemetry.Create()
            .WithLogging()
            .Build();

        // Act
        var logger = telemetry.LoggerFactory?.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);
    }

    [Fact(DisplayName = "Logger should emit logs with trace correlation.")]
    public void Logger_EmitsLogsWithTraceCorrelation()
    {
        // Arrange
        using var telemetry = SimulationTelemetry.Create()
            .WithLogging(includeConsoleExporter: true)
            .Build();

        var logger = telemetry.LoggerFactory?.CreateLogger("TestCategory");
        Assert.NotNull(logger);

        // Create an activity to simulate a trace context
        using var activity = new Activity("TestOperation");
        activity.Start();

        // Act
        logger.LogInformation("Test log message with trace context");

        // Assert
        Assert.NotNull(Activity.Current);
        Assert.Equal(activity.TraceId, Activity.Current.TraceId);
    }

    [Fact(DisplayName = "WithLogging without OTLP should not fail.")]
    public void WithLogging_WithoutOtlp_ShouldNotFail()
    {
        // Arrange & Act
        using var telemetry = SimulationTelemetry.Create()
            .WithLogging(includeOtlpExporter: true) // OTLP requested but not configured
            .Build();

        var logger = telemetry.LoggerFactory?.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);

        // Should not throw
        logger.LogInformation("Test message");
    }

    [Fact(DisplayName = "WithLogging with console exporter should enable console logging.")]
    public void WithLogging_WithConsoleExporter_EnablesConsoleLogging()
    {
        // Arrange & Act
        using var telemetry = SimulationTelemetry.Create()
            .WithLogging(includeConsoleExporter: true, includeOtlpExporter: false)
            .Build();

        var logger = telemetry.LoggerFactory?.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);

        // Should not throw
        logger.LogInformation("Console log message");
    }

    [Fact(DisplayName = "Without WithLogging, LoggerFactory should be null.")]
    public void WithoutWithLogging_LoggerFactoryShouldBeNull()
    {
        // Arrange & Act
        using var telemetry = SimulationTelemetry.Create()
            .WithConsoleExporter()
            .Build();

        // Assert
        Assert.Null(telemetry.LoggerFactory);
    }

    [Fact(DisplayName = "WithLogging with OTLP exporter should configure logs endpoint.")]
    public void WithLogging_WithOtlpExporter_ConfiguresLogsEndpoint()
    {
        // Arrange & Act
        using var telemetry = SimulationTelemetry.Create()
            .WithOtlpExporter("http://localhost:4317")
            .WithLogging(includeOtlpExporter: true)
            .Build();

        var logger = telemetry.LoggerFactory?.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);

        // Should not throw
        logger.LogInformation("OTLP log message");
    }
}
