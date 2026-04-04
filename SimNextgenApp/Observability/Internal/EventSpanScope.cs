using System.Diagnostics;

namespace SimNextgenApp.Observability.Internal;

/// <summary>
/// Disposable wrapper for event spans that automatically restores Activity.Current context.
/// Ensures proper context management when trace context is disabled.
/// </summary>
internal sealed class EventSpanScope : IDisposable
{
    private readonly Activity? _span;
    private readonly Activity? _savedContext;

    internal EventSpanScope(Activity? span, Activity? savedContext)
    {
        _span = span;
        _savedContext = savedContext;
    }

    /// <summary>
    /// Gets the event span, or null if tracing is disabled.
    /// </summary>
    public Activity? Span => _span;

    public void Dispose()
    {
        _span?.Dispose();
        // Automatically restore previous Activity.Current context
        // This prevents context leakage when trace context is disabled
        if (_savedContext != null)
        {
            Activity.Current = _savedContext;
        }
    }
}
