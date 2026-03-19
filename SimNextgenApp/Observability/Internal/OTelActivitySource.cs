using System.Diagnostics;
using SimNextgenApp.Core;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Observability.Internal;

/// <summary>
/// Internal wrapper for OpenTelemetry ActivitySource to standardize simulation tracing.
/// Handled automatically by the SimulationEngine.
/// </summary>
internal sealed class OTelActivitySource
{
    private static readonly ActivitySource _sharedSource = new(SimulationTelemetry.ActivitySourceName);
    private readonly VolumeEstimator? _volumeEstimator;

    /// <summary>
    /// Initializes a new instance of the OTelActivitySource class.
    /// </summary>
    /// <param name="volumeEstimator">Optional volume estimator for tracking span creation.</param>
    public OTelActivitySource(VolumeEstimator? volumeEstimator = null)
    {
        _volumeEstimator = volumeEstimator;
    }

    /// <summary>
    /// Ensures tracing allocations are skipped if there's no listener observing them.
    /// </summary>
    public bool IsEnabled => _sharedSource.HasListeners();

    public Activity? CreateSimulationSpan(SimulationProfile profile)
    {
        if (!IsEnabled) return null;

        var activity = _sharedSource.StartActivity($"SimulationRun-{profile.Name}", ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("sna.simulation.id", profile.RunId);
            activity.SetTag("sna.simulation.name", profile.Name);

            // Track span creation for volume estimation
            _volumeEstimator?.RecordSpan();
        }
        return activity;
    }

    public Activity? CreateEventSpan(string eventName, long clockTime, string eventId, bool isWarmupPhase)
    {
        if (!IsEnabled) return null;

        var activity = _sharedSource.StartActivity(eventName, ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("sna.event.id", eventId);
            activity.SetTag("sna.simulation.time", clockTime);
            activity.SetTag("sna.simulation.warmup", isWarmupPhase);

            // Track span creation for volume estimation
            _volumeEstimator?.RecordSpan();
        }
        return activity;
    }
}
