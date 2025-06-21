using System.Text.Json;

namespace SimNextgenApp.Statistics;

/// <summary>
/// Encapsulates the statistical results and final state of a completed simulation run.
/// </summary>
/// <param name="ProfileRunId">The unique identifier for the simulation profile run.</param>
/// <param name="ProfileName">The name of the simulation profile run.</param>
/// <param name="FinalClockTime">The simulation clock time when the run ended.</param>
/// <param name="ExecutedEventCount">The total number of events executed during the run.</param>
/// <param name="RealTimeDuration">The actual real-world time it took to execute the simulation run.</param>
/// <param name="ModelId">The ID of the model that was run.</param>
/// <param name="ModelName">The name of the model that was run.</param>
public record SimulationResult(
    Guid ProfileRunId,
    string ProfileName,
    double FinalClockTime,
    long ExecutedEventCount,
    TimeSpan RealTimeDuration,
    long ModelId,
    string ModelName
)
{
    /// <summary>
    /// Provides a detailed, multi-line string representation of the simulation result,
    /// perfect for logging to the console.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("");
        sb.AppendLine("------------------- Simulation Run Results -------------------");
        sb.AppendLine($"  Profile Name:           {ProfileName} (ID: {ProfileRunId})");
        sb.AppendLine($"  Model Name:             {ModelName} (ID: {ModelId})");
        sb.AppendLine($"  Final Simulation Time:  {FinalClockTime:F4}");
        sb.AppendLine($"  Executed Event Count:   {ExecutedEventCount:N0}");
        sb.AppendLine($"  Real-Time Execution:    {RealTimeDuration.TotalMilliseconds:F2} ms");
        sb.AppendLine("--------------------------------------------------------------");
        return sb.ToString();
    }

    /// <summary>
    /// Serialises the result to a JSON string.
    /// </summary>
    /// <param name="prettyPrint">If true, formats the JSON with indentation for readability.</param>
    public string ToJson(bool prettyPrint = true)
    {
        var options = new JsonSerializerOptions { WriteIndented = prettyPrint };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Returns the CSV header row for a collection of results.
    /// </summary>
    public static string GetCsvHeader()
    {
        return "ProfileId,ProfileName,ModelId,ModelName,FinalClockTime,ExecutedEventCount,RealTimeDurationMs";
    }

    /// <summary>
    // Returns the result as a single CSV data row.
    /// </summary>
    public string ToCsvRow()
    {
        return $"{ProfileRunId},{ProfileName},{ModelId},{ModelName},{FinalClockTime},{ExecutedEventCount},{RealTimeDuration.TotalMilliseconds}";
    }
}