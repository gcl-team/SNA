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
/// </summary>
public sealed class OtlpExporterConfiguration
{
    /// <summary>
    /// Gets the OTLP endpoint URI.
    /// </summary>
    public Uri Endpoint { get; }

    /// <summary>
    /// Gets the headers required for authentication.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    private OtlpExporterConfiguration(Uri endpoint, IReadOnlyDictionary<string, string> headers)
    {
        Endpoint = endpoint;
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
            OtlpBackend.GrafanaCloud => CreateGrafanaCloudConfig(apiKey),
            OtlpBackend.Datadog => CreateDatadogConfig(apiKey, region),
            OtlpBackend.Honeycomb => CreateHoneycombConfig(apiKey),
            _ => throw new ArgumentException($"Unsupported backend: {backend}", nameof(backend))
        };
    }

    /// <summary>
    /// Creates configuration for Grafana Cloud.
    /// </summary>
    /// <param name="apiKey">The Grafana Cloud API key (format: "instanceId:apiToken").</param>
    /// <returns>An <see cref="OtlpExporterConfiguration"/> instance.</returns>
    /// <remarks>
    /// API key format: "instanceId:apiToken" where instanceId is your Grafana Cloud instance ID.
    /// Endpoint: https://otlp-gateway-prod-us-central-0.grafana.net/otlp
    /// </remarks>
    public static OtlpExporterConfiguration CreateGrafanaCloudConfig(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        var endpoint = new Uri("https://otlp-gateway-prod-us-central-0.grafana.net/otlp");
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(apiKey))}"
        };

        return new OtlpExporterConfiguration(endpoint, headers);
    }

    /// <summary>
    /// Creates configuration for Datadog.
    /// </summary>
    /// <param name="apiKey">The Datadog API key.</param>
    /// <param name="region">The Datadog region (e.g., "us1", "eu1", "us3", "us5"). Defaults to "us1".</param>
    /// <returns>An <see cref="OtlpExporterConfiguration"/> instance.</returns>
    /// <remarks>
    /// Supported regions: us1, us3, us5, eu1, ap1, gov
    /// Endpoint format: https://api.{region}.datadoghq.com/api/v2/otlp
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

        return new OtlpExporterConfiguration(endpoint, headers);
    }

    /// <summary>
    /// Creates configuration for Honeycomb.
    /// </summary>
    /// <param name="apiKey">The Honeycomb API key.</param>
    /// <returns>An <see cref="OtlpExporterConfiguration"/> instance.</returns>
    /// <remarks>
    /// Endpoint: https://api.honeycomb.io/v1/traces
    /// </remarks>
    public static OtlpExporterConfiguration CreateHoneycombConfig(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        var endpoint = new Uri("https://api.honeycomb.io/v1/traces");
        var headers = new Dictionary<string, string>
        {
            ["x-honeycomb-team"] = apiKey
        };

        return new OtlpExporterConfiguration(endpoint, headers);
    }
}
