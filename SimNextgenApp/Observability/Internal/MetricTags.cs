namespace SimNextgenApp.Observability.Internal;

/// <summary>
/// Shared metric tags for the observers.
/// </summary>
internal static class MetricTags
{
    // Boxed once, because passing the bool straight into a tag boxes it on every measurement,
    // even when no MeterProvider is listening.
    private static readonly KeyValuePair<string, object?> WarmupTrue = new("sna.simulation.warmup", true);
    private static readonly KeyValuePair<string, object?> WarmupFalse = new("sna.simulation.warmup", false);

    /// <summary>
    /// Gets the <c>sna.simulation.warmup</c> tag for the given warmup state.
    /// </summary>
    internal static KeyValuePair<string, object?> Warmup(bool isWarmup) => isWarmup ? WarmupTrue : WarmupFalse;
}
