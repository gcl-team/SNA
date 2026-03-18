using System.Diagnostics;
using SimNextgenApp.Core;

namespace SimNextgenApp.Observability.Internal;

/// <summary>
/// Internal wrapper for OpenTelemetry ActivitySource to standardize simulation tracing.
/// Handled automatically by the SimulationEngine.
/// </summary>
internal sealed class OTelActivitySource
{
    private readonly ActivitySource _source;

    public OTelActivitySource()
    {
        _source = new ActivitySource(SimulationTelemetry.ActivitySourceName);
    }

    /// <summary>
    /// Ensures tracing allocations are skipped if there's no listener observing them.
    /// </summary>
    public bool IsEnabled => _source.HasListeners();

    public Activity? CreateSimulationSpan(SimulationProfile profile)
    {
        if (!IsEnabled) return null;

        var activity = _source.StartActivity($"SimulationRun-{profile.Name}", ActivityKind.Internal);
        activity?.SetTag("sna.simulation.id", profile.RunId);
        activity?.SetTag("sna.simulation.name", profile.Name);
        return activity;
    }

    public Activity? CreateEventSpan(string eventName, long clockTime, string eventId, bool isWarmupPhase)
    {
        if (!IsEnabled) return null;

        var activity = _source.StartActivity(eventName, ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("sna.event.id", eventId);
            activity.SetTag("sna.simulation.time", clockTime);
            activity.SetTag("sna.simulation.warmup", isWarmupPhase);
        }
        return activity;
    }
}
