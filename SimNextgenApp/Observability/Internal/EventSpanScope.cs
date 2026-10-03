using System.Diagnostics;

namespace SimNextgenApp.Observability.Internal;

/// <summary>
/// Disposable wrapper for event spans that automatically restores Activity.Current context.
/// Ensures proper context management when trace context is disabled.
/// </summary>
/// <remarks>
/// A struct, so the engine allocates nothing per event when tracing is disabled.
/// The default value holds no span and its <see cref="Dispose"/> does nothing.
/// </remarks>
internal readonly struct EventSpanScope : IDisposable
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
