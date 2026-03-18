using System.Diagnostics.Metrics;

namespace SimNextgenApp.Observability.Internal;

/// <summary>
/// Internal wrapper for OpenTelemetry Meters to standardize simulation metric naming.
/// </summary>
/// <remarks>
/// This class enforces the "sna." prefix convention. Callers should pass raw metric names
/// (e.g., "server.loads_completed") and this class will add the "sna." prefix automatically.
/// If a name already starts with "sna.", it will be used as-is to allow flexibility for advanced users.
/// </remarks>
internal sealed class OTelMeterProvider
{
    private static readonly Meter _meter = new(SimulationTelemetry.MeterName);

    public static Meter GetMeter()
    {
        return _meter;
    }

    /// <summary>
    /// Creates a counter with automatic "sna." prefix normalization.
    /// If the name already starts with "sna.", it is used as-is. Otherwise, "sna." is prepended.
    /// </summary>
    /// <param name="name">Metric name (e.g., "server.loads_completed" or "sna.server.loads_completed").</param>
    /// <param name="unit">Optional unit of measurement.</param>
    /// <param name="description">Optional description of the counter.</param>
    /// <returns>A counter instrument with the "sna." prefix ensured.</returns>
    /// <exception cref="ArgumentException">Thrown if name is null or empty.</exception>
    public static Counter<T> CreateCounter<T>(string name, string unit = "", string description = "") where T : struct
    {
        string normalizedName = NormalizeMetricName(name);
        return _meter.CreateCounter<T>(normalizedName, unit, description);
    }

    /// <summary>
    /// Creates a histogram with automatic "sna." prefix normalization.
    /// If the name already starts with "sna.", it is used as-is. Otherwise, "sna." is prepended.
    /// </summary>
    /// <param name="name">Metric name (e.g., "server.sojourn_time" or "sna.server.sojourn_time").</param>
    /// <param name="unit">Optional unit of measurement.</param>
    /// <param name="description">Optional description of the histogram.</param>
    /// <returns>A histogram instrument with the "sna." prefix ensured.</returns>
    /// <exception cref="ArgumentException">Thrown if name is null or empty.</exception>
    public static Histogram<T> CreateHistogram<T>(string name, string unit = "", string description = "") where T : struct
    {
        string normalizedName = NormalizeMetricName(name);
        return _meter.CreateHistogram<T>(normalizedName, unit, description);
    }

    /// <summary>
    /// Normalizes a metric name to ensure it has the "sna." prefix.
    /// </summary>
    /// <param name="name">The input metric name.</param>
    /// <returns>The metric name with "sna." prefix ensured.</returns>
    /// <exception cref="ArgumentException">Thrown if name is null or empty.</exception>
    private static string NormalizeMetricName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));
        }

        // If already prefixed with "sna." (case-insensitive), return as-is
        if (name.StartsWith("sna.", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        // Otherwise, add the prefix
        return $"sna.{name}";
    }
}
