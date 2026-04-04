using System.Diagnostics;
using SimNextgenApp.Core;
using SimNextgenApp.Observability.Advanced;
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
    private readonly CardinalityGuard? _cardinalityGuard;
    private readonly bool _enableTraceContext;

    /// <summary>
    /// Initializes a new instance of the OTelActivitySource class.
    /// </summary>
    /// <param name="volumeEstimator">Optional volume estimator for tracking span creation.</param>
    /// <param name="cardinalityGuard">Optional cardinality guard for monitoring attribute cardinality.</param>
    /// <param name="enableTraceContext">Whether to enable trace context propagation.</param>
    public OTelActivitySource(
        VolumeEstimator? volumeEstimator = null,
        CardinalityGuard? cardinalityGuard = null,
        bool enableTraceContext = false)
    {
        _volumeEstimator = volumeEstimator;
        _cardinalityGuard = cardinalityGuard;
        _enableTraceContext = enableTraceContext;
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

    /// <summary>
    /// Creates a span for the warmup period of the simulation.
    /// </summary>
    /// <param name="warmupEndTime">The simulation time when warmup ends.</param>
    /// <returns>An activity representing the warmup span, or null if tracing is not enabled.</returns>
    public Activity? CreateWarmupSpan(long warmupEndTime)
    {
        if (!IsEnabled) return null;

        var activity = _sharedSource.StartActivity("Warmup", ActivityKind.Internal);
        if (activity != null)
        {
            activity.SetTag("sna.simulation.warmup_end_time", warmupEndTime);
            _volumeEstimator?.RecordSpan();
        }
        return activity;
    }

    public Activity? CreateEventSpan(string eventName, long clockTime, string eventId, bool isWarmupPhase)
    {
        if (!IsEnabled) return null;

        Activity? activity;

        // When trace context is disabled, create independent root spans (better performance for high-volume events)
        // When enabled, create hierarchical spans as children of the simulation run (shows causality)
        if (_enableTraceContext)
        {
            // Create child span parented to Activity.Current (the simulation span)
            activity = _sharedSource.StartActivity(eventName, ActivityKind.Internal);
        }
        else
        {
            // Create root span by temporarily suppressing Activity.Current during creation
            // This ensures event spans are truly independent and not parented to the simulation span
            // The created span becomes Activity.Current so downstream code (observers) can access its tags
            var savedCurrent = Activity.Current;
            Activity.Current = null;
            activity = _sharedSource.StartActivity(eventName, ActivityKind.Internal);

            // If creation failed, restore previous context
            if (activity == null)
            {
                Activity.Current = savedCurrent;
            }
            // Otherwise, leave the new activity as Current - caller will dispose it to restore context
        }

        if (activity != null)
        {
            activity.SetTag("sna.event.id", eventId);
            activity.SetTag("sna.simulation.time", clockTime);
            activity.SetTag("sna.simulation.warmup", isWarmupPhase);

            // Track attribute cardinality
            _cardinalityGuard?.RecordAttributeValue("sna.event.type", eventName);

            // Track span creation for volume estimation
            _volumeEstimator?.RecordSpan();
        }
        return activity;
    }
}
