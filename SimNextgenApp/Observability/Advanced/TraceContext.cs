using System.Diagnostics;

namespace SimNextgenApp.Observability.Advanced;

/// <summary>
/// Advanced tracing utilities for managing trace context and span relationships.
/// Provides helpers for W3C Trace Context propagation and cross-service correlation.
/// </summary>
public static class TraceContext
{
    /// <summary>
    /// Extracts W3C Trace Context from a dictionary of headers.
    /// </summary>
    /// <param name="headers">The headers containing trace context (e.g., "traceparent", "tracestate").</param>
    /// <returns>An <see cref="ActivityContext"/> if trace context was found; otherwise, default.</returns>
    /// <remarks>
    /// W3C Trace Context format:
    /// traceparent: 00-{trace-id}-{parent-id}-{trace-flags}
    /// tracestate: key1=value1,key2=value2
    /// </remarks>
    public static ActivityContext ExtractContext(IDictionary<string, string> headers)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));

        if (!headers.TryGetValue("traceparent", out var traceparent))
            return default;

        try
        {
            var parts = traceparent.Split('-');
            if (parts.Length < 4)
                return default;

            // Parse version (00)
            if (parts[0] != "00")
                return default;

            // Parse trace ID (32 hex characters)
            ActivityTraceId traceId;
            try
            {
                traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
            }
            catch
            {
                return default;
            }

            // Parse parent/span ID (16 hex characters)
            ActivitySpanId spanId;
            try
            {
                spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
            }
            catch
            {
                return default;
            }

            // Parse trace flags (2 hex characters)
            var traceFlags = ActivityTraceFlags.None;
            if (parts[3].Length >= 2 && int.TryParse(parts[3][..2], System.Globalization.NumberStyles.HexNumber, null, out var flags))
            {
                traceFlags = (ActivityTraceFlags)flags;
            }

            // Extract tracestate if present
            string? traceState = null;
            if (headers.TryGetValue("tracestate", out var traceStateValue))
            {
                traceState = traceStateValue;
            }

            return new ActivityContext(traceId, spanId, traceFlags, traceState, isRemote: true);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Injects W3C Trace Context from the current activity into a dictionary of headers.
    /// </summary>
    /// <param name="headers">The headers dictionary to inject trace context into.</param>
    /// <param name="activity">The activity to extract trace context from (default: Activity.Current).</param>
    /// <remarks>
    /// Injects:
    /// - traceparent: 00-{trace-id}-{parent-id}-{trace-flags}
    /// - tracestate: (if present in the activity)
    /// </remarks>
    public static void InjectContext(IDictionary<string, string> headers, Activity? activity = null)
    {
        if (headers == null)
            throw new ArgumentNullException(nameof(headers));

        activity ??= Activity.Current;
        if (activity == null)
            return;

        var traceId = activity.TraceId.ToHexString();
        var spanId = activity.SpanId.ToHexString();
        var traceFlags = ((int)activity.ActivityTraceFlags).ToString("x2");

        headers["traceparent"] = $"00-{traceId}-{spanId}-{traceFlags}";

        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            headers["tracestate"] = activity.TraceStateString;
        }
    }

    /// <summary>
    /// Creates a new span that is linked to a specific parent context.
    /// Useful for distributed tracing scenarios where spans need to be correlated across services.
    /// </summary>
    /// <param name="activitySource">The activity source to create the span from.</param>
    /// <param name="name">The name of the span.</param>
    /// <param name="parentContext">The parent activity context to link to.</param>
    /// <param name="kind">The kind of activity (default: Internal).</param>
    /// <param name="tags">Optional tags to add to the span.</param>
    /// <returns>A new <see cref="Activity"/> linked to the parent context; null if not enabled.</returns>
    /// <remarks>
    /// This creates a linked span rather than a child span, which is useful when:
    /// - Correlating spans across service boundaries
    /// - Creating spans from message queue consumers
    /// - Implementing fan-out/fan-in patterns
    /// </remarks>
    public static Activity? CreateLinkedSpan(
        ActivitySource activitySource,
        string name,
        ActivityContext parentContext,
        ActivityKind kind = ActivityKind.Internal,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        if (activitySource == null)
            throw new ArgumentNullException(nameof(activitySource));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Span name cannot be null or whitespace.", nameof(name));

        if (parentContext == default)
            return activitySource.StartActivity(name, kind);

        var links = new[] { new ActivityLink(parentContext) };
        var activity = activitySource.StartActivity(name, kind, parentContext: default, tags, links);

        return activity;
    }

    /// <summary>
    /// Gets the current trace ID as a string, or null if no active trace.
    /// </summary>
    /// <returns>The hex-encoded trace ID, or null if no activity is active.</returns>
    public static string? GetCurrentTraceId()
    {
        return Activity.Current?.TraceId.ToHexString();
    }

    /// <summary>
    /// Gets the current span ID as a string, or null if no active span.
    /// </summary>
    /// <returns>The hex-encoded span ID, or null if no activity is active.</returns>
    public static string? GetCurrentSpanId()
    {
        return Activity.Current?.SpanId.ToHexString();
    }

    /// <summary>
    /// Checks if distributed tracing is currently active.
    /// </summary>
    /// <returns>True if there is an active activity with a valid trace ID; otherwise, false.</returns>
    public static bool IsTracingActive()
    {
        return Activity.Current != null && Activity.Current.TraceId != default;
    }
}
