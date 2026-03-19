using System.Diagnostics;

namespace SimNextgenApp.Observability;

/// <summary>
/// A convenience API replacing custom tracers, interacting with OpenTelemetry underneath.
/// </summary>
public static class SimulationTracer
{
    private static readonly ActivitySource _source =
        new ActivitySource(SimulationTelemetry.ActivitySourceName);

    /// <summary>
    /// Trace an event manually if you're not using standard SNA automatic tracing.
    /// (SNA Engine automatically traces internally to OTel)
    /// </summary>
    public static void TraceEvent(string eventName, long simulationTime, IDictionary<string, object?>? tags = null)
    {
        if (!_source.HasListeners()) return;

        using var activity = _source.StartActivity(eventName);
        if (activity == null) return;

        activity.SetTag("sna.simulation.time", simulationTime);
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag($"sna.event.detail.{tag.Key.ToLowerInvariant()}", tag.Value);
            }
        }
    }
}
