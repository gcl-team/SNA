namespace SimNextgenApp.Statistics;

/// <summary>
/// Writes the contents of a <see cref="MemoryTracer"/> to the console or a specified <see cref="TextWriter"/> in a
/// human-readable format.
/// </summary>
public static class SimulationTracerExtensions
{
    /// <summary>
    /// Writes the contents of a MemoryTracer to the console in a human-readable format.
    /// </summary>
    /// <param name="tracer">The MemoryTracer instance containing the records.</param>
    /// <param name="writer">Optional TextWriter to direct output (defaults to Console.Out).</param>
    public static void PrintToConsole(this MemoryTracer tracer, TextWriter? writer = null)
    {
        writer ??= Console.Out;

        writer.WriteLine("\n--- Detailed Event Trace ---");
        foreach (var record in tracer.Records)
        {
            var detailsString = "";
            if (record.Details != null && record.Details.Any())
            {
                detailsString = " | " + string.Join(", ", record.Details.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            }

            writer.WriteLine($"[TRACE] {record.Point,-18} | Time: {record.ClockTime:F2} | Event: {record.EventType,-25} (ID: {record.EventId}){detailsString}");
        }
        writer.WriteLine("----------------------------");
    }
}
