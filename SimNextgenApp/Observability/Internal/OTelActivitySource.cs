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

    // Pre-boxed so tagging the warmup state on every event span does not allocate a box
    private static readonly object _boxedTrue = true;
    private static readonly object _boxedFalse = false;

    private readonly ActivitySource _source;
    private readonly VolumeEstimator? _volumeEstimator;
    private readonly CardinalityGuard? _cardinalityGuard;
    private readonly bool _enableTraceContext;

    /// <summary>
    /// Initializes a new instance of the OTelActivitySource class.
    /// </summary>
    /// <param name="volumeEstimator">Optional volume estimator for tracking span creation.</param>
    /// <param name="cardinalityGuard">Optional cardinality guard for monitoring attribute cardinality.</param>
    /// <param name="enableTraceContext">Whether to enable trace context propagation.</param>
    /// <param name="source">The source to create spans from. Defaults to the shared SNA source; tests pass their own.</param>
    public OTelActivitySource(
        VolumeEstimator? volumeEstimator = null,
        CardinalityGuard? cardinalityGuard = null,
        bool enableTraceContext = false,
        ActivitySource? source = null)
    {
        _source = source ?? _sharedSource;
        _volumeEstimator = volumeEstimator;
        _cardinalityGuard = cardinalityGuard;
        _enableTraceContext = enableTraceContext;
    }

    /// <summary>
    /// Ensures tracing allocations are skipped if there's no listener observing them.
    /// </summary>
    public bool IsEnabled => _source.HasListeners();

    public Activity? CreateSimulationSpan(SimulationProfile profile)
    {
        if (!IsEnabled) return null;

        var activity = _source.StartActivity($"SimulationRun-{profile.Name}", ActivityKind.Internal);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("sna.simulation.id", profile.RunId);
            activity.SetTag("sna.simulation.name", profile.Name);
        }

        // Track span creation for volume estimation
        if (activity is { Recorded: true }) _volumeEstimator?.RecordSpan();
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

        var activity = _source.StartActivity("Warmup", ActivityKind.Internal);
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("sna.simulation.warmup_end_time", warmupEndTime);
        }

        if (activity is { Recorded: true }) _volumeEstimator?.RecordSpan();
        return activity;
    }

    /// <summary>
    /// Creates an event span wrapped in a scope that automatically restores Activity.Current context.
    /// When trace context is disabled, the scope ensures the simulation/warmup span context is restored
    /// after the event span is disposed, preventing context leakage.
    /// </summary>
    /// <remarks>
    /// A sampler that drops a span still returns an <see cref="Activity"/> (with
    /// <see cref="Activity.IsAllDataRequested"/> false) so that context keeps flowing. Such spans get only
    /// the warmup tag, because observers read it from <see cref="Activity.Current"/> to label their
    /// metrics. All other tags are skipped, and the span is not counted towards volume or cardinality,
    /// because it will never be exported.
    /// </remarks>
    /// <returns>An EventSpanScope that must be disposed to restore context properly.</returns>
    public EventSpanScope CreateEventSpan(string eventName, long clockTime, long eventId, bool isWarmupPhase)
    {
        if (!IsEnabled) return new EventSpanScope(null, null);

        Activity? activity;
        Activity? savedContext = null;

        // When trace context is disabled, create independent root spans (better performance for high-volume events)
        // When enabled, create hierarchical spans as children of the simulation run (shows causality)
        if (_enableTraceContext)
        {
            // Create child span parented to Activity.Current (the simulation span)
            activity = _source.StartActivity(eventName, ActivityKind.Internal);
        }
        else
        {
            // Create root span by temporarily suppressing Activity.Current during creation
            // This ensures event spans are truly independent and not parented to the simulation span
            // The created span becomes Activity.Current so downstream code (observers) can access its tags
            // EventSpanScope will automatically restore savedContext when disposed
            savedContext = Activity.Current;
            Activity.Current = null;
            activity = _source.StartActivity(eventName, ActivityKind.Internal);

            // If creation failed, restore previous context immediately
            if (activity == null)
            {
                Activity.Current = savedContext;
                savedContext = null; // No need to restore again
            }
        }

        if (activity != null)
        {
            activity.SetTag("sna.simulation.warmup", isWarmupPhase ? _boxedTrue : _boxedFalse);

            if (activity.IsAllDataRequested)
            {
                activity.SetTag("sna.event.id", eventId.ToString());
                activity.SetTag("sna.simulation.time", clockTime);
            }

            if (activity.Recorded)
            {
                // Track attribute cardinality
                _cardinalityGuard?.RecordAttributeValue("sna.event.type", eventName);

                // Track span creation for volume estimation
                _volumeEstimator?.RecordSpan();
            }
        }
        return new EventSpanScope(activity, savedContext);
    }
}
