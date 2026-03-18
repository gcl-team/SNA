using System.Diagnostics.Metrics;

namespace SimNextgenApp.Observability.Internal;

/// <summary>
/// Internal wrapper for OpenTelemetry Meters to standardize simulation metric naming.
/// </summary>
internal sealed class OTelMeterProvider
{
    private static readonly Meter _meter = new Meter(SimulationTelemetry.MeterName);

    public static Meter GetMeter()
    {
        return _meter;
    }

    public static Counter<T> CreateCounter<T>(string name, string unit = "", string description = "") where T : struct
    {
        return _meter.CreateCounter<T>($"sna.{name}", unit, description);
    }

    public static Histogram<T> CreateHistogram<T>(string name, string unit = "", string description = "") where T : struct
    {
        return _meter.CreateHistogram<T>($"sna.{name}", unit, description);
    }
}
