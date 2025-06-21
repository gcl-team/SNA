namespace SimNextgenApp.Statistics;

/// <summary>
/// A structured record representing a single point in the simulation execution history.
/// </summary>
public record TraceRecord(
    TracePoint Point,
    double ClockTime,
    long EventId,
    string EventType,
    IDictionary<string, object>? Details = null
);

/// <summary>
/// Represents the different trace points in the simulation lifecycle.
/// </summary>
public enum TracePoint
{
    EventScheduled,
    EventExecuting,
    EventCompleted
}