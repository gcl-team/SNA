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
    private string _prometheusHostname = "localhost";
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
    /// <param name="port">The port to listen on (default: 9090).</param>
    /// <param name="hostname">The hostname to bind to (default: "localhost"). Use "+" to bind to all interfaces, but this may require URL ACL registration on Windows.</param>
    /// <remarks>
    /// <para><strong>Important:</strong> HttpListener may require URL ACL registration or administrator privileges on Windows.</para>
    /// <para>By default, binds to localhost only. For remote Prometheus scraping, set hostname to "+" or a specific IP address.</para>
    /// <para>To register URL ACL on Windows: <c>netsh http add urlacl url=http://+:{port}/ user=DOMAIN\username</c></para>
    /// <para>If the listener fails to start, check Windows Event Log or consider running with elevated privileges.</para>
    /// </remarks>
    public SimulationTelemetryBuilder WithPrometheusExporter(int port = 9090, string hostname = "localhost")
    {
        _usePrometheusExporter = true;
        _prometheusPort = port;
        _prometheusHostname = hostname;
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
    /// <exception cref="InvalidOperationException">Thrown if Prometheus HttpListener fails to start (e.g., insufficient permissions, port already in use).</exception>
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
            meterBuilder.AddPrometheusHttpListener(options =>
                options.UriPrefixes = new string[] { $"http://{_prometheusHostname}:{_prometheusPort}/" });
        }

        // Apply advanced user customizations
        _configureTracer?.Invoke(tracerBuilder);
        _configureMeter?.Invoke(meterBuilder);

        var tracerProvider = tracerBuilder.Build();

        MeterProvider? meterProvider = null;
        try
        {
            meterProvider = meterBuilder.Build();
        }
        catch (Exception ex) when (_usePrometheusExporter)
        {
            tracerProvider?.Dispose();
            throw new InvalidOperationException(
                $"Failed to start Prometheus HttpListener on http://{_prometheusHostname}:{_prometheusPort}/. " +
                $"This may be due to insufficient permissions (URL ACL registration required on Windows), " +
                $"the port already being in use, or an invalid hostname. " +
                $"See inner exception for details.", ex);
        }

        return new SimulationTelemetry(tracerProvider, meterProvider);
    }
}
