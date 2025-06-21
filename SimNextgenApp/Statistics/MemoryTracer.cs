namespace SimNextgenApp.Statistics;

/// <summary>
/// An in-memory implementation of <see cref="ISimulationTracer"/> that stores all
/// trace records in a public list.
/// <para>
/// <strong>WARNING:</strong> This tracer is intended for debugging, unit testing, and
/// simulations with a small, finite number of events. For long-running simulations
/// or those with millions of events, this will consume a large amount of memory
/// and can lead to an <see cref="OutOfMemoryException"/>.
/// </para>
/// <para>
/// For large-scale runs, consider using a file-based or streaming tracer instead.
/// </para>
/// </summary>
public class MemoryTracer : ISimulationTracer
{
    /// <summary>
    /// Gets the list of all trace records captured during the simulation run.
    /// </summary>
    public List<TraceRecord> Records { get; } = [];

    /// <inheritdoc/>
    public void Trace(TraceRecord record)
    {
        Records.Add(record);
    }
}
