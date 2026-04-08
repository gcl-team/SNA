using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SimNextgenApp.Observability.Sampling;
using SimNextgenApp.Observability.VolumeEstimation;
using SimNextgenApp.Observability.Advanced;
using SimNextgenApp.Observability.Exporters;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Modeling.Resource;

namespace SimNextgenApp.Observability;

/// <summary>
/// A fluent builder and facade for configuring OpenTelemetry in the simulation.
/// </summary>
public sealed class SimulationTelemetry : IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private readonly MeterProvider? _meterProvider;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly VolumeEstimator? _volumeEstimator;
    private readonly CardinalityGuard? _cardinalityGuard;
    private readonly bool _enableTraceContext;

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
    /// Gets the cardinality guard for monitoring attribute cardinality.
    /// Returns null if cardinality guard is not enabled.
    /// </summary>
    public CardinalityGuard? CardinalityGuard => _cardinalityGuard;

    /// <summary>
    /// Gets whether trace context propagation is enabled.
    /// </summary>
    public bool EnableTraceContext => _enableTraceContext;

    /// <summary>
    /// Gets the logger factory for creating loggers.
    /// Returns null if logging is not enabled.
    /// </summary>
    public ILoggerFactory? LoggerFactory => _loggerFactory;

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

    internal SimulationTelemetry(TracerProvider? tracerProvider, MeterProvider? meterProvider, ILoggerFactory? loggerFactory, VolumeEstimator? volumeEstimator, CardinalityGuard? cardinalityGuard, bool enableTraceContext)
    {
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
        _loggerFactory = loggerFactory;
        _volumeEstimator = volumeEstimator;
        _cardinalityGuard = cardinalityGuard;
        _enableTraceContext = enableTraceContext;
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
    public bool Flush(int timeoutMilliseconds = 5000)
    {
        bool tracerFlushed = _tracerProvider?.ForceFlush(timeoutMilliseconds) ?? true;
        bool meterFlushed = _meterProvider?.ForceFlush(timeoutMilliseconds) ?? true;
        return tracerFlushed && meterFlushed;
    }

    /// <summary>
    /// Creates an observer for a server component.
    /// Each observer owns its own meter to allow independent disposal and avoid memory leaks.
    /// </summary>
    public ServerObserver<TLoad> ObserveServer<TLoad>(IServer<TLoad> server)
    {
        // Create owned meter to allow observer to be disposed independently
        var meter = new System.Diagnostics.Metrics.Meter(MeterName);
        return new ServerObserver<TLoad>(server, meter, ownsMeter: true, _volumeEstimator);
    }

    /// <summary>
    /// Creates an observer for a queue component.
    /// Each observer owns its own meter to allow independent disposal and avoid memory leaks.
    /// </summary>
    public QueueObserver<TLoad> ObserveQueue<TLoad>(ISimQueue<TLoad> queue)
        where TLoad : notnull
    {
        // Create owned meter to allow observer to be disposed independently
        var meter = new System.Diagnostics.Metrics.Meter(MeterName);
        return new QueueObserver<TLoad>(queue, meter, ownsMeter: true, _volumeEstimator);
    }

    /// <summary>
    /// Creates an observer for a generator component.
    /// Each observer owns its own meter to allow independent disposal and avoid memory leaks.
    /// </summary>
    public GeneratorObserver<TLoad> ObserveGenerator<TLoad>(IGenerator<TLoad> generator)
        where TLoad : notnull
    {
        // Create owned meter to allow observer to be disposed independently
        var meter = new System.Diagnostics.Metrics.Meter(MeterName);
        return new GeneratorObserver<TLoad>(generator, meter, ownsMeter: true, _volumeEstimator);
    }

    /// <summary>
    /// Creates an observer for a resource pool component.
    /// Each observer owns its own meter to allow independent disposal and avoid memory leaks.
    /// </summary>
    public ResourceObserver<TResource> ObserveResource<TResource>(IResourcePool<TResource> resourcePool)
        where TResource : notnull
    {
        // Create owned meter to allow observer to be disposed independently
        var meter = new System.Diagnostics.Metrics.Meter(MeterName);
        return new ResourceObserver<TResource>(resourcePool, meter, ownsMeter: true, _volumeEstimator);
    }

    /// <summary>
    /// Creates an observer for the whole simulation.
    /// Each observer owns its own meter to allow independent disposal and avoid memory leaks.
    /// </summary>
    /// <param name="engine">The simulation engine to observe.</param>
    /// <param name="warmupEndTime">Optional warmup end time (in simulation time units).</param>
    public SimulationObserver ObserveSimulation(Core.SimulationEngine engine, long? warmupEndTime = null)
    {
        // Create owned meter to allow observer to be disposed independently
        var meter = new System.Diagnostics.Metrics.Meter(MeterName);
        return new SimulationObserver(engine, meter, ownsMeter: true, _volumeEstimator, warmupEndTime);
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
        _loggerFactory?.Dispose();
        _volumeEstimator?.Dispose();
        _cardinalityGuard?.Dispose();
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
    private int? _cardinalityThreshold;
    private bool _enableTraceContext;
    private OtlpBackend? _otlpBackend;
    private string? _otlpApiKey;
    private string? _otlpRegion;
    private bool _useLogging;
    private bool _useLoggingConsoleExporter;
    private bool _useLoggingOtlpExporter;
    private string _serviceName = "SimNextgenApp";
    private string _serviceVersion = "1.0.0";

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
    /// <param name="hostname">The hostname to bind to (default: "localhost"). Supports HttpListener wildcards: "+" (all interfaces) or "*" (all interfaces with URL ACL required on Windows), or specific IP addresses/hostnames.</param>
    /// <remarks>
    /// <para><strong>Important:</strong> HttpListener may require URL ACL registration or administrator privileges on Windows.</para>
    /// <para>By default, binds to localhost only. For remote Prometheus scraping, use "+" or "*" to bind to all interfaces, or specify a specific IP address.</para>
    /// <para>To register URL ACL on Windows: <c>netsh http add urlacl url=http://+:{port}/ user=DOMAIN\username</c></para>
    /// <para>If the listener fails to start, check Windows Event Log or consider running with elevated privileges.</para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if port is not between 1 and 65535.</exception>
    /// <exception cref="ArgumentException">Thrown if hostname is null, empty, or contains invalid characters for HttpListener.</exception>
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

        // Allow HttpListener wildcards explicitly ("+", "*") which are valid for HttpListener but not Uri
        if (hostname != "+" && hostname != "*")
        {
            // Validate that hostname forms a valid URI
            string testUri = $"http://{hostname}:{port}/";
            if (!Uri.TryCreate(testUri, UriKind.Absolute, out _))
            {
                throw new ArgumentException($"Hostname '{hostname}' does not form a valid URI for Prometheus.", nameof(hostname));
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

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out _))
        {
            throw new ArgumentException($"OTLP endpoint '{endpoint}' is not a valid absolute URI.", nameof(endpoint));
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
    /// Enables cardinality guard to monitor and warn about high-cardinality attributes.
    /// </summary>
    /// <param name="threshold">The maximum number of unique values per attribute before warning (default: 1000).</param>
    public SimulationTelemetryBuilder WithCardinalityGuard(int threshold = 1000)
    {
        if (threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be greater than zero.");
        _cardinalityThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Enables advanced trace context propagation for distributed tracing scenarios.
    /// </summary>
    public SimulationTelemetryBuilder WithTraceContext()
    {
        _enableTraceContext = true;
        return this;
    }

    /// <summary>
    /// Enables OpenTelemetry logging integration with automatic log-trace correlation.
    /// </summary>
    /// <param name="includeConsoleExporter">Whether to export logs to console (default: false).</param>
    /// <param name="includeOtlpExporter">Whether to export logs via OTLP (default: true if OTLP exporter is configured).</param>
    /// <remarks>
    /// When enabled, logs will be automatically correlated with active traces via Activity.Current.
    /// Logs will include TraceId and SpanId when emitted within an active span context.
    /// </remarks>
    public SimulationTelemetryBuilder WithLogging(bool includeConsoleExporter = false, bool includeOtlpExporter = true)
    {
        _useLogging = true;
        _useLoggingConsoleExporter = includeConsoleExporter;
        _useLoggingOtlpExporter = includeOtlpExporter;
        return this;
    }

    /// <summary>
    /// Sets the service name and version for telemetry resource attributes.
    /// </summary>
    /// <param name="serviceName">The name of the service (default: "SimNextgenApp").</param>
    /// <param name="serviceVersion">The version of the service (default: "1.0.0").</param>
    public SimulationTelemetryBuilder WithServiceInfo(string serviceName, string? serviceVersion = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be null or empty.", nameof(serviceName));

        _serviceName = serviceName;
        _serviceVersion = serviceVersion ?? "1.0.0";
        return this;
    }

    /// <summary>
    /// Adds OTLP exporter with backend-specific configuration presets.
    /// </summary>
    /// <param name="backend">The backend provider (GrafanaCloud, Datadog, Honeycomb).</param>
    /// <param name="apiKey">The API key or token for authentication.</param>
    /// <param name="region">Optional region for region-specific endpoints (e.g., Datadog).</param>
    public SimulationTelemetryBuilder WithOtlpExporter(OtlpBackend backend, string apiKey, string? region = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");

        _otlpBackend = backend;
        _otlpApiKey = apiKey;
        _otlpRegion = region;
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
    /// <exception cref="InvalidOperationException">Thrown if Prometheus HttpListener fails to start (e.g., insufficient permissions, port already in use), or if conflicting OTLP exporters are configured.</exception>
    public SimulationTelemetry Build()
    {
        // Validate that both OTLP exporter configurations are not used simultaneously
        if (_useOtlpExporter && _otlpBackend.HasValue)
        {
            throw new InvalidOperationException(
                "Cannot configure both WithOtlpExporter(string endpoint) and WithOtlpExporter(OtlpBackend, apiKey, ...). " +
                "These methods are mutually exclusive. Use only one OTLP exporter configuration.");
        }

        var tracerBuilder = Sdk.CreateTracerProviderBuilder()
            .AddSource(SimulationTelemetry.ActivitySourceName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(serviceName: _serviceName, serviceVersion: _serviceVersion));

        var meterBuilder = Sdk.CreateMeterProviderBuilder()
            .AddMeter(SimulationTelemetry.MeterName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(serviceName: _serviceName, serviceVersion: _serviceVersion));

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

        // Configure backend-specific OTLP exporter
        if (_otlpBackend.HasValue && !string.IsNullOrEmpty(_otlpApiKey))
        {
            var otlpConfig = OtlpExporterConfiguration.ConfigureForBackend(
                _otlpBackend.Value,
                _otlpApiKey,
                region: _otlpRegion);

            tracerBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = otlpConfig.TracesEndpoint; // Signal-specific endpoint
                options.Protocol = OtlpExportProtocol.HttpProtobuf; // Backend presets use OTLP/HTTP
                options.Headers = string.Join(",",
                    otlpConfig.Headers.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            });

            meterBuilder.AddOtlpExporter(options =>
            {
                options.Endpoint = otlpConfig.MetricsEndpoint; // Signal-specific endpoint
                options.Protocol = OtlpExportProtocol.HttpProtobuf; // Backend presets use OTLP/HTTP
                options.Headers = string.Join(",",
                    otlpConfig.Headers.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            });
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

        // Create cardinality guard if requested
        CardinalityGuard? cardinalityGuard = _cardinalityThreshold.HasValue
            ? new CardinalityGuard(_cardinalityThreshold.Value)
            : null;

        // Create logger factory with OpenTelemetry integration if logging is enabled
        ILoggerFactory? loggerFactory = null;
        if (_useLogging)
        {
            loggerFactory = LoggerFactory.Create(loggingBuilder =>
            {
                loggingBuilder.AddOpenTelemetry(options =>
                {
                    // Enable automatic log-trace correlation via Activity.Current
                    options.IncludeScopes = true;
                    options.IncludeFormattedMessage = true;
                    options.ParseStateValues = true;

                    // Apply the same service name and version to logs
                    options.SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: _serviceName, serviceVersion: _serviceVersion));

                    // Add console exporter if requested
                    if (_useLoggingConsoleExporter)
                    {
                        options.AddConsoleExporter();
                    }

                    // Add OTLP exporter if requested and OTLP is configured
                    if (_useLoggingOtlpExporter && (_useOtlpExporter || _otlpBackend.HasValue))
                    {
                        if (_useOtlpExporter)
                        {
                            options.AddOtlpExporter(otlpOptions =>
                                otlpOptions.Endpoint = new Uri(_otlpEndpoint));
                        }
                        else if (_otlpBackend.HasValue && !string.IsNullOrEmpty(_otlpApiKey))
                        {
                            var otlpConfig = OtlpExporterConfiguration.ConfigureForBackend(
                                _otlpBackend.Value,
                                _otlpApiKey,
                                region: _otlpRegion);

                            options.AddOtlpExporter(otlpOptions =>
                            {
                                otlpOptions.Endpoint = otlpConfig.LogsEndpoint;
                                otlpOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                                otlpOptions.Headers = string.Join(",",
                                    otlpConfig.Headers.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                            });
                        }
                    }
                });
            });
        }

        return new SimulationTelemetry(tracerProvider, meterProvider, loggerFactory, volumeEstimator, cardinalityGuard, _enableTraceContext);
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
