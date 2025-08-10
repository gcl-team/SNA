using Serilog;

namespace SimNextgenApp.Statistics;

/// <summary>
/// An implementation of <see cref="ISimulationTracer"/> that sends structured
/// trace records to a Serilog logger, which can be configured to write to Seq.
/// </summary>
public class SeqTracer(ILogger serilogger) : ISimulationTracer
{
    // The message template defines the "human-readable" part of the log event.
    // The properties are all sent as structured data.
    private const string TraceTemplate =
        "[{TracePoint}] Event {EventType} (ID: {EventId}) at T={ClockTime:F4}";

    /// <inheritdoc/>
    public void Trace(TraceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        // This is the magic of Serilog. We provide a message template and then
        // pass the properties. Serilog sends them to Seq as structured data.
        // The Details dictionary is attached as a structured sub-object.
        serilogger
            .ForContext("Details", record.Details, destructureObjects: true)
            .Information(
                TraceTemplate,
                record.Point,
                record.EventType,
                record.EventId,
                record.ClockTime);
    }
}
