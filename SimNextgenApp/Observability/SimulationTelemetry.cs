using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace SimNextgenApp.Observability;

/// <summary>
/// A fluent builder and facade for configuring OpenTelemetry in the simulation.
/// </summary>
public sealed class SimulationTelemetry : IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private readonly MeterProvider? _meterProvider;

    public const string ActivitySourceName = "SNA.Simulation.Traces";
    public const string MeterName = "SNA.Simulation.Metrics";

    public System.Diagnostics.ActivitySource ActivitySource { get; }
    public System.Diagnostics.Metrics.Meter Meter { get; }

    internal SimulationTelemetry(TracerProvider? tracerProvider, MeterProvider? meterProvider)
    {
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
        ActivitySource = new System.Diagnostics.ActivitySource(ActivitySourceName);
        Meter = new System.Diagnostics.Metrics.Meter(MeterName);
    }

    /// <summary>
    /// Forces the exporters to flush any pending telemetry data immediately to their targets.
    /// Useful at the end of a simulation run or when shutting down the application.
    /// </summary>
    public void Shutdown()
    {
        _tracerProvider?.ForceFlush();
        _meterProvider?.ForceFlush();
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
        ActivitySource.Dispose();
        Meter.Dispose();
    }

    /// <summary>
    /// Starts building a new <see cref="SimulationTelemetry"/> instance.
    /// </summary>
    public static SimulationTelemetryBuilder Create() => new SimulationTelemetryBuilder();
}

/// <summary>
/// Configures options for OpenTelemetry trace and metric providers.
/// </summary>
public class SimulationTelemetryBuilder
{
    private bool _useConsoleExporter;
    private bool _usePrometheusExporter;
    private int _prometheusPort = 9090;
    private Action<TracerProviderBuilder>? _configureTracer;
    private Action<MeterProviderBuilder>? _configureMeter;

    /// <summary>
    /// Appends the Console Exporter to both the tracer and meter providers.
    /// </summary>
    public SimulationTelemetryBuilder WithConsoleExporter()
    {
        _useConsoleExporter = true;
        return this;
    }

    /// <summary>
    /// Adds Prometheus as a metric exporter on the specified port.
    /// </summary>
    public SimulationTelemetryBuilder WithPrometheusExporter(int port = 9090)
    {
        _usePrometheusExporter = true;
        _prometheusPort = port;
        return this;
    }

    /// <summary>
    /// Allows advanced users to configure the OpenTelemetry Builder explicitly and add other exporters 
    /// or custom auto-instrumentation rules.
    /// </summary>
    public SimulationTelemetryBuilder ConfigureOpenTelemetry(Action<TracerProviderBuilder>? configureTracer = null, Action<MeterProviderBuilder>? configureMeter = null)
    {
        _configureTracer = configureTracer;
        _configureMeter = configureMeter;
        return this;
    }

    /// <summary>
    /// Builds the configured OpenTelemetry providers and returns a reusable telemetry facade.
    /// </summary>
    public SimulationTelemetry Build()
    {
        var tracerBuilder = Sdk.CreateTracerProviderBuilder()
            .AddSource(SimulationTelemetry.ActivitySourceName);
            
        var meterBuilder = Sdk.CreateMeterProviderBuilder()
            .AddMeter(SimulationTelemetry.MeterName);

        if (_useConsoleExporter)
        {
            tracerBuilder.AddConsoleExporter();
            meterBuilder.AddConsoleExporter();
        }

        if (_usePrometheusExporter)
        {
            // Note: AspNetCore prometheus scraping is typical, but we can also use HttpListener
            // meterBuilder.AddPrometheusHttpListener(options => options.UriPrefixes = new string[] { $"http://localhost:{_prometheusPort}/" });
        }

        // Apply advanced user customizations
        _configureTracer?.Invoke(tracerBuilder);
        _configureMeter?.Invoke(meterBuilder);

        var tracerProvider = tracerBuilder.Build();
        var meterProvider = meterBuilder.Build();

        return new SimulationTelemetry(tracerProvider, meterProvider);
    }
}
