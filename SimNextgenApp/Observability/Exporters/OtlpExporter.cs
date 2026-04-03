namespace SimNextgenApp.Observability.Exporters;

/// <summary>
/// Supported OTLP backend providers.
/// </summary>
public enum OtlpBackend
{
    /// <summary>
    /// Grafana Cloud OTLP endpoint.
    /// </summary>
    GrafanaCloud,

    /// <summary>
    /// Datadog OTLP endpoint.
    /// </summary>
    Datadog,

    /// <summary>
    /// Honeycomb OTLP endpoint.
    /// </summary>
    Honeycomb
}

/// <summary>
/// Configuration for OTLP exporters with backend-specific presets.
/// All presets use OTLP/HTTP (HttpProtobuf) protocol.
/// </summary>
public sealed class OtlpExporterConfiguration
{
    /// <summary>
    /// Gets the OTLP traces endpoint URI (OTLP/HTTP format).
    /// </summary>
    public Uri TracesEndpoint { get; }

    /// <summary>
    /// Gets the OTLP metrics endpoint URI (OTLP/HTTP format).
    /// </summary>
    public Uri MetricsEndpoint { get; }

    /// <summary>
    /// Gets the headers required for authentication.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    private OtlpExporterConfiguration(Uri tracesEndpoint, Uri metricsEndpoint, IReadOnlyDictionary<string, string> headers)
    {
        TracesEndpoint = tracesEndpoint;
        MetricsEndpoint = metricsEndpoint;
        Headers = headers;
    }

    /// <summary>
    /// Configures OTLP exporter for a specific backend provider.
    /// </summary>
    /// <param name="backend">The backend provider.</param>
    /// <param name="apiKey">The API key or token for authentication.</param>
    /// <param name="region">Optional region for region-specific endpoints (e.g., Datadog).</param>
    /// <returns>An <see cref="OtlpExporterConfiguration"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when apiKey is null or empty.</exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported backend is specified.</exception>
    public static OtlpExporterConfiguration ConfigureForBackend(
        OtlpBackend backend,
        string apiKey,
        string? region = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey), "API key cannot be null or empty.");

        return backend switch
        {
            OtlpBackend.GrafanaCloud => CreateGrafanaCloudConfig(apiKey, region),
            OtlpBackend.Datadog => CreateDatadogConfig(apiKey, region),
            OtlpBackend.Honeycomb => CreateHoneycombConfig(apiKey, region),
            _ => throw new ArgumentException($"Unsupported backend: {backend}", nameof(backend))
        };
    }

    /// <summary>
    /// Creates configuration for Grafana Cloud.
    /// </summary>
    /// <param name="apiKey">The Grafana Cloud API key (format: "instanceId:apiToken").</param>
    /// <param name="region">The Grafana Cloud region (e.g., "us-central-0", "eu-west-0", "ap-southeast-0"). Defaults to "us-central-0".</param>
    /// <returns>An <see cref="OtlpExporterConfiguration"/> instance.</returns>
    /// <remarks>
    /// API key format: "instanceId:apiToken" where instanceId is your Grafana Cloud instance ID.
    /// Supported regions: us-central-0, eu-west-0, ap-southeast-0, au-southeast-0
    /// Endpoint format: https://otlp-gateway-prod-{region}.grafana.net/otlp (OTLP/HTTP)
    /// Grafana Cloud uses a unified endpoint for both traces and metrics.
    /// Protocol: OTLP/HTTP (HttpProtobuf)
    /// </remarks>
    public static OtlpExporterConfiguration CreateGrafanaCloudConfig(string apiKey, string? region = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        region ??= "us-central-0"; // Default to US Central region
        var endpoint = new Uri($"https://otlp-gateway-prod-{region}.grafana.net/otlp");
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(apiKey))}"
        };

        // Grafana Cloud uses unified endpoint for both signals
        return new OtlpExporterConfiguration(endpoint, endpoint, headers);
    }

    /// <summary>
    /// Creates configuration for Datadog.
    /// </summary>
    /// <param name="apiKey">The Datadog API key.</param>
    /// <param name="region">The Datadog region (e.g., "us1", "eu1", "us3", "us5"). Defaults to "us1".</param>
    /// <returns>An <see cref="OtlpExporterConfiguration"/> instance.</returns>
    /// <remarks>
    /// Supported regions: us1, us3, us5, eu1, ap1, gov
    /// Endpoint format: https://api.{region}.datadoghq.com/api/v2/otlp (OTLP/HTTP)
    /// Datadog uses a unified endpoint for both traces and metrics.
    /// Protocol: OTLP/HTTP (HttpProtobuf)
    /// </remarks>
    public static OtlpExporterConfiguration CreateDatadogConfig(string apiKey, string? region = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        region ??= "us1"; // Default to US1 region
        var endpoint = new Uri($"https://api.{region}.datadoghq.com/api/v2/otlp");
        var headers = new Dictionary<string, string>
        {
            ["dd-api-key"] = apiKey
        };

        // Datadog uses unified endpoint for both signals
        return new OtlpExporterConfiguration(endpoint, endpoint, headers);
    }

    /// <summary>
    /// Creates configuration for Honeycomb.
    /// </summary>
    /// <param name="apiKey">The Honeycomb API key.</param>
    /// <param name="region">The Honeycomb region. Use "eu1" for EU, or null/"us" for US (default).</param>
    /// <returns>An <see cref="OtlpExporterConfiguration"/> instance.</returns>
    /// <remarks>
    /// Supported regions: us (default), eu1
    /// US traces endpoint: https://api.honeycomb.io/v1/traces (OTLP/HTTP)
    /// US metrics endpoint: https://api.honeycomb.io/v1/metrics (OTLP/HTTP)
    /// EU traces endpoint: https://api.eu1.honeycomb.io/v1/traces (OTLP/HTTP)
    /// EU metrics endpoint: https://api.eu1.honeycomb.io/v1/metrics (OTLP/HTTP)
    /// Honeycomb uses signal-specific endpoints.
    /// Protocol: OTLP/HTTP (HttpProtobuf)
    /// </remarks>
    public static OtlpExporterConfiguration CreateHoneycombConfig(string apiKey, string? region = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        // Honeycomb US has no region in the URL, EU uses "eu1" prefix
        var baseUrl = string.IsNullOrEmpty(region) || region.Equals("us", StringComparison.OrdinalIgnoreCase)
            ? "https://api.honeycomb.io"
            : $"https://api.{region}.honeycomb.io";

        var tracesEndpoint = new Uri($"{baseUrl}/v1/traces");
        var metricsEndpoint = new Uri($"{baseUrl}/v1/metrics");
        var headers = new Dictionary<string, string>
        {
            ["x-honeycomb-team"] = apiKey
        };

        // Honeycomb uses signal-specific endpoints
        return new OtlpExporterConfiguration(tracesEndpoint, metricsEndpoint, headers);
    }
}
