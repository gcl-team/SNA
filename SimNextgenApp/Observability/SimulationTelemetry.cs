using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SimNextgenApp.Observability.Sampling;
using SimNextgenApp.Observability.VolumeEstimation;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Modeling.Queue;

namespace SimNextgenApp.Observability;

/// <summary>
/// A fluent builder and facade for configuring OpenTelemetry in the simulation.
/// </summary>
public sealed class SimulationTelemetry : IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private readonly MeterProvider? _meterProvider;
    private readonly VolumeEstimator? _volumeEstimator;

    public const string ActivitySourceName = "SNA.Simulation.Traces";
    public const string MeterName = "SNA.Simulation.Metrics";

    public System.Diagnostics.ActivitySource ActivitySource { get; }
    public System.Diagnostics.Metrics.Meter Meter { get; }

    /// <summary>
    /// Gets the volume estimator for tracking telemetry volume.
    /// Returns null if volume estimation is not enabled.
    /// </summary>
    public VolumeEstimator? VolumeEstimator => _volumeEstimator;

    /// <summary>
    /// Gets the current rate of spans per second.
    /// Returns 0 if volume estimation is not enabled.
    /// </summary>
    public double SpansPerSecond => _volumeEstimator?.SpansPerSecond ?? 0;

    /// <summary>
    /// Gets the current rate of metric data points per second.
    /// Returns 0 if volume estimation is not enabled.
    /// </summary>
    public double MetricDataPointsPerSecond => _volumeEstimator?.MetricDataPointsPerSecond ?? 0;

    internal SimulationTelemetry(TracerProvider? tracerProvider, MeterProvider? meterProvider, VolumeEstimator? volumeEstimator)
    {
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
        _volumeEstimator = volumeEstimator;
        ActivitySource = new System.Diagnostics.ActivitySource(ActivitySourceName);
        Meter = new System.Diagnostics.Metrics.Meter(MeterName);
    }

    /// <summary>
    /// Forces the exporters to flush any pending telemetry data immediately to their targets.
    /// Useful at the end of a simulation run to ensure all data is exported before the application exits.
    /// </summary>
    /// <remarks>
    /// This method only flushes pending data and does not stop the providers from accepting new telemetry.
    /// For full shutdown and cleanup, call Dispose() or use the telemetry object in a using statement.
    /// </remarks>
    public void Flush()
    {
        _tracerProvider?.ForceFlush();
        _meterProvider?.ForceFlush();
    }

    /// <summary>
    /// Creates an observer for a server component.
    /// </summary>
    public SimulationObserver<TLoad> ObserveServer<TLoad>(IServer<TLoad> server)
    {
        return new SimulationObserver<TLoad>(server, Meter, ownsMeter: false);
    }

    /// <summary>
    /// Creates an observer for a queue component.
    /// </summary>
    public QueueObserver<TLoad> ObserveQueue<TLoad>(ISimQueue<TLoad> queue)
        where TLoad : notnull
    {
        return new QueueObserver<TLoad>(queue, Meter, ownsMeter: false);
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
        _volumeEstimator?.Dispose();
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
    private bool _useOtlpExporter;
    private string _otlpEndpoint = "http://localhost:4317";
    private SamplingConfiguration? _samplingConfig;
    private VolumeThresholds? _volumeThresholds;
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if port is not between 1 and 65535.</exception>
    /// <exception cref="ArgumentException">Thrown if hostname is null, empty, or contains invalid characters.</exception>
    public SimulationTelemetryBuilder WithPrometheusExporter(int port = 9090, string hostname = "localhost")
    {
        if (port < 1 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new ArgumentException("Hostname cannot be null or empty.", nameof(hostname));
        }

        // Basic validation: reject hostnames with invalid characters for URI
        // Allow "+", "*", "localhost", IP addresses, and valid hostnames
        if (hostname != "+" && hostname != "*" && hostname != "localhost")
        {
            // Check for obviously invalid characters (spaces, control chars)
            if (hostname.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            {
                throw new ArgumentException($"Hostname '{hostname}' contains invalid characters.", nameof(hostname));
            }
        }

        _usePrometheusExporter = true;
        _prometheusPort = port;
        _prometheusHostname = hostname;
        return this;
    }

    /// <summary>
    /// Adds OTLP (OpenTelemetry Protocol) exporter for traces and metrics.
    /// </summary>
    /// <param name="endpoint">The OTLP endpoint URL (default: http://localhost:4317).</param>
    public SimulationTelemetryBuilder WithOtlpExporter(string endpoint = "http://localhost:4317")
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("OTLP endpoint cannot be null or empty.", nameof(endpoint));
        }

        _useOtlpExporter = true;
        _otlpEndpoint = endpoint;
        return this;
    }

    /// <summary>
    /// Configures trace sampling to reduce telemetry volume.
    /// </summary>
    /// <param name="samplingConfig">The sampling configuration to use.</param>
    public SimulationTelemetryBuilder WithSampling(SamplingConfiguration samplingConfig)
    {
        _samplingConfig = samplingConfig ?? throw new ArgumentNullException(nameof(samplingConfig));
        return this;
    }

    /// <summary>
    /// Configures trace sampling with a simple percentage rate.
    /// </summary>
    /// <param name="rate">Sampling rate between 0.0 (0%) and 1.0 (100%).</param>
    public SimulationTelemetryBuilder WithSampling(double rate)
    {
        _samplingConfig = SamplingConfiguration.Random(rate);
        return this;
    }

    /// <summary>
    /// Enables volume estimation to track and warn about high telemetry volume.
    /// </summary>
    /// <param name="thresholds">Volume thresholds for triggering warnings. If null, uses default thresholds.</param>
    public SimulationTelemetryBuilder WithVolumeEstimation(VolumeThresholds? thresholds = null)
    {
        _volumeThresholds = thresholds ?? VolumeThresholds.Default();
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

        // Apply sampling configuration if specified
        if (_samplingConfig != null)
        {
            tracerBuilder.SetSampler(CreateSampler(_samplingConfig));
        }

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

        if (_useOtlpExporter)
        {
            tracerBuilder.AddOtlpExporter(options =>
                options.Endpoint = new Uri(_otlpEndpoint));
            meterBuilder.AddOtlpExporter(options =>
                options.Endpoint = new Uri(_otlpEndpoint));
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
        catch (Exception ex)
        {
            // Always dispose tracerProvider on any meter build failure to prevent resource leak
            tracerProvider?.Dispose();

            // Provide helpful error message for Prometheus-specific failures
            if (_usePrometheusExporter)
            {
                throw new InvalidOperationException(
                    $"Failed to start Prometheus HttpListener on http://{_prometheusHostname}:{_prometheusPort}/. " +
                    $"This may be due to insufficient permissions (URL ACL registration required on Windows), " +
                    $"the port already being in use, or an invalid hostname. " +
                    $"See inner exception for details.", ex);
            }

            // Rethrow for other exporter failures
            throw;
        }

        // Create volume estimator if requested
        VolumeEstimator? volumeEstimator = _volumeThresholds != null
            ? new VolumeEstimator(_volumeThresholds)
            : null;

        return new SimulationTelemetry(tracerProvider, meterProvider, volumeEstimator);
    }

    private static Sampler CreateSampler(SamplingConfiguration config)
    {
        return config.Strategy switch
        {
            SamplingStrategy.AlwaysOn => new AlwaysOnSampler(),
            SamplingStrategy.AlwaysOff => new AlwaysOffSampler(),
            SamplingStrategy.Random => new TraceIdRatioBasedSampler(config.SamplingRate),
            SamplingStrategy.ParentBased => new ParentBasedSampler(new TraceIdRatioBasedSampler(config.SamplingRate)),
            _ => new AlwaysOnSampler()
        };
    }
}
