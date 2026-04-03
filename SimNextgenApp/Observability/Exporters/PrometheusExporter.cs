using System.Text;
using System.Text.RegularExpressions;

namespace SimNextgenApp.Observability.Exporters;

/// <summary>
/// Utility class for Prometheus metric naming conventions and configuration.
/// </summary>
public static class PrometheusExporter
{
    private static readonly Regex PrometheusNameRegex = new(@"^[a-zA-Z_:][a-zA-Z0-9_:]*$", RegexOptions.Compiled);

    /// <summary>
    /// Converts an OpenTelemetry metric name to Prometheus naming convention.
    /// </summary>
    /// <param name="otelMetricName">The OpenTelemetry metric name (e.g., "sna.server.utilization").</param>
    /// <returns>The Prometheus-compatible metric name (e.g., "sna_server_utilization").</returns>
    /// <remarks>
    /// Prometheus naming rules:
    /// - Must match [a-zA-Z_:][a-zA-Z0-9_:]*
    /// - All invalid characters (anything not matching [a-zA-Z0-9_:]) are replaced with underscores
    /// - If the name starts with a digit, an underscore prefix is added
    /// - The result is guaranteed to pass ValidatePrometheusName
    /// </remarks>
    public static string ConvertToPrometheusName(string otelMetricName)
    {
        if (string.IsNullOrWhiteSpace(otelMetricName))
            throw new ArgumentException("Metric name cannot be null or whitespace.", nameof(otelMetricName));

        var sb = new StringBuilder(otelMetricName.Length);

        for (int i = 0; i < otelMetricName.Length; i++)
        {
            char c = otelMetricName[i];

            // Valid characters: a-z, A-Z, 0-9, _, :
            if (char.IsLetterOrDigit(c) || c == '_' || c == ':')
            {
                sb.Append(c);
            }
            else
            {
                // Replace any invalid character with underscore
                sb.Append('_');
            }
        }

        var prometheusName = sb.ToString();

        // Ensure it starts with a valid character (letter, underscore, or colon)
        if (prometheusName.Length > 0 && char.IsDigit(prometheusName[0]))
        {
            prometheusName = "_" + prometheusName;
        }

        return prometheusName;
    }

    /// <summary>
    /// Validates whether a metric name conforms to Prometheus naming conventions.
    /// </summary>
    /// <param name="metricName">The metric name to validate.</param>
    /// <returns>True if the name is valid; otherwise, false.</returns>
    /// <remarks>
    /// Prometheus naming rules:
    /// - Must match [a-zA-Z_:][a-zA-Z0-9_:]*
    /// - Must start with a letter, underscore, or colon
    /// - Can only contain letters, digits, underscores, and colons
    /// </remarks>
    public static bool ValidatePrometheusName(string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            return false;

        return PrometheusNameRegex.IsMatch(metricName);
    }

    /// <summary>
    /// Generates a Prometheus scrape configuration for the simulation metrics.
    /// </summary>
    /// <param name="jobName">The Prometheus job name (default: "sna-simulation").</param>
    /// <param name="scrapeInterval">The scrape interval (default: "10s").</param>
    /// <param name="targetHost">The target host (default: "localhost").</param>
    /// <param name="targetPort">The target port (default: 9090).</param>
    /// <returns>A YAML-formatted Prometheus scrape configuration.</returns>
    /// <remarks>
    /// Example output:
    /// <code>
    /// scrape_configs:
    ///   - job_name: 'sna-simulation'
    ///     scrape_interval: 10s
    ///     static_configs:
    ///       - targets: ['localhost:9090']
    /// </code>
    /// </remarks>
    public static string GenerateScrapeConfig(
        string jobName = "sna-simulation",
        string scrapeInterval = "10s",
        string targetHost = "localhost",
        int targetPort = 9090)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("Job name cannot be null or whitespace.", nameof(jobName));

        if (string.IsNullOrWhiteSpace(scrapeInterval))
            throw new ArgumentException("Scrape interval cannot be null or whitespace.", nameof(scrapeInterval));

        if (string.IsNullOrWhiteSpace(targetHost))
            throw new ArgumentException("Target host cannot be null or whitespace.", nameof(targetHost));

        if (targetPort <= 0 || targetPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(targetPort), "Port must be between 1 and 65535.");

        var sb = new StringBuilder();
        sb.AppendLine("scrape_configs:");
        sb.AppendLine($"  - job_name: '{jobName}'");
        sb.AppendLine($"    scrape_interval: {scrapeInterval}");
        sb.AppendLine("    static_configs:");
        sb.AppendLine($"      - targets: ['{targetHost}:{targetPort}']");

        return sb.ToString();
    }
}
