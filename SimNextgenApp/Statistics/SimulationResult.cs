using System.Text.Json;
using SimNextgenApp.Core.Utilities;

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
/// <param name="TimeUnit">The simulation time unit used for this run.</param>
public record SimulationResult(
    Guid ProfileRunId,
    string ProfileName,
    long FinalClockTime,
    long ExecutedEventCount,
    TimeSpan RealTimeDuration,
    long ModelId,
    string ModelName,
    SimulationTimeUnit TimeUnit
)
{
    /// <summary>
    /// Provides a detailed, multi-line string representation of the simulation result,
    /// perfect for logging to the console.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();

        // FinalClockTime is already in the correct units (specified by TimeUnit)
        // Format directly as long to avoid precision loss
        string unitName = TimeUnitConverter.GetUnitDisplayName(TimeUnit);

        sb.AppendLine("");
        sb.AppendLine("------------------- Simulation Run Results -------------------");
        sb.AppendLine($"  Profile Name:           {ProfileName} (ID: {ProfileRunId})");
        sb.AppendLine($"  Model Name:             {ModelName} (ID: {ModelId})");
        sb.AppendLine($"  Final Simulation Time:  {FinalClockTime:N0} {unitName}");
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
        return "ProfileId,ProfileName,ModelId,ModelName,FinalClockTime,TimeUnit,ExecutedEventCount,RealTimeDurationMs";
    }

    /// <summary>
    /// Returns the result as a single CSV data row.
    /// The TimeUnit column contains the enum name (e.g., "Milliseconds") for round-trip parsing.
    /// </summary>
    public string ToCsvRow()
    {
        // Output enum name for machine parsing, not display symbol
        return $"{ProfileRunId},{ProfileName},{ModelId},{ModelName},{FinalClockTime},{TimeUnit},{ExecutedEventCount},{RealTimeDuration.TotalMilliseconds}";
    }
}