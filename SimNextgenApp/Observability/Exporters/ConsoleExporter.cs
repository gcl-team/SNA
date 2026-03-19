using System.Diagnostics;
using System.Text;

namespace SimNextgenApp.Observability.Exporters;

/// <summary>
/// Enhanced console exporter for OpenTelemetry telemetry data.
/// Provides pretty-printed tables for metrics and trace visualization.
/// </summary>
public static class ConsoleExporter
{
    /// <summary>
    /// Formats and prints a collection of metrics to the console in a table format.
    /// </summary>
    /// <param name="metrics">Collection of metric data to display.</param>
    public static void PrintMetricsTable(IEnumerable<MetricData> metrics)
    {
        if (metrics == null || !metrics.Any())
        {
            Console.WriteLine("No metrics to display.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                          SIMULATION METRICS                               ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════════╣");

        var metricsList = metrics.ToList();
        var nameWidth = Math.Max(20, metricsList.Max(m => m.Name.Length));
        var valueWidth = 15;
        var unitWidth = 10;

        // Header
        Console.WriteLine($"║ {"Metric Name".PadRight(nameWidth)} │ {"Value".PadLeft(valueWidth)} │ {"Unit".PadLeft(unitWidth)} ║");
        Console.WriteLine($"╠═{new string('═', nameWidth)}═╪═{new string('═', valueWidth)}═╪═{new string('═', unitWidth)}═╣");

        // Rows
        foreach (var metric in metricsList)
        {
            var name = metric.Name.Length > nameWidth ? metric.Name.Substring(0, nameWidth - 3) + "..." : metric.Name;
            var value = FormatValue(metric.Value, valueWidth);
            var unit = metric.Unit.Length > unitWidth ? metric.Unit.Substring(0, unitWidth - 3) + "..." : metric.Unit;

            Console.WriteLine($"║ {name.PadRight(nameWidth)} │ {value.PadLeft(valueWidth)} │ {unit.PadLeft(unitWidth)} ║");
        }

        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    /// <summary>
    /// Formats and prints trace information to the console with hierarchical visualization.
    /// </summary>
    /// <param name="traces">Collection of trace data to display.</param>
    public static void PrintTracesHierarchy(IEnumerable<TraceData> traces)
    {
        if (traces == null || !traces.Any())
        {
            Console.WriteLine("No traces to display.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                          SIMULATION TRACES                                ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var tracesList = traces.ToList();

        // Build hierarchy
        var rootTraces = tracesList.Where(t => t.ParentSpanId == null).ToList();
        var childTraces = tracesList.Where(t => t.ParentSpanId != null)
            .GroupBy(t => t.ParentSpanId)
            .ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var root in rootTraces)
        {
            PrintTraceNode(root, childTraces, 0);
        }

        Console.WriteLine();
    }

    private static void PrintTraceNode(TraceData trace, Dictionary<string, List<TraceData>> childMap, int depth)
    {
        var indent = new string(' ', depth * 2);
        var prefix = depth > 0 ? "├─ " : "";
        var durationMs = trace.Duration.TotalMilliseconds;

        Console.WriteLine($"{indent}{prefix}{trace.Name} ({durationMs:F2}ms)");

        if (trace.Attributes.Any())
        {
            var attrIndent = new string(' ', (depth + 1) * 2);
            foreach (var attr in trace.Attributes)
            {
                Console.WriteLine($"{attrIndent}  {attr.Key}: {attr.Value}");
            }
        }

        if (childMap.TryGetValue(trace.SpanId, out var children))
        {
            foreach (var child in children.OrderBy(c => c.StartTime))
            {
                PrintTraceNode(child, childMap, depth + 1);
            }
        }
    }

    /// <summary>
    /// Prints a summary of simulation statistics.
    /// </summary>
    /// <param name="summary">Simulation summary data.</param>
    public static void PrintSimulationSummary(SimulationSummary summary)
    {
        if (summary == null)
        {
            Console.WriteLine("No summary to display.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                        SIMULATION SUMMARY                                 ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Duration:          {FormatDuration(summary.Duration).PadLeft(54)} ║");
        Console.WriteLine($"║ Events Executed:   {summary.EventsExecuted.ToString("N0").PadLeft(54)} ║");
        Console.WriteLine($"║ Spans Created:     {summary.SpansCreated.ToString("N0").PadLeft(54)} ║");
        Console.WriteLine($"║ Metrics Recorded:  {summary.MetricsRecorded.ToString("N0").PadLeft(54)} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static string FormatValue(object value, int width)
    {
        if (value == null) return "N/A";

        return value switch
        {
            double d => d.ToString("F2"),
            float f => f.ToString("F2"),
            int i => i.ToString("N0"),
            long l => l.ToString("N0"),
            _ => value.ToString() ?? "N/A"
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:F2} days";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F2} hours";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F2} minutes";
        if (duration.TotalSeconds >= 1)
            return $"{duration.TotalSeconds:F2} seconds";
        return $"{duration.TotalMilliseconds:F2} ms";
    }
}

/// <summary>
/// Represents a single metric data point for console display.
/// </summary>
public record MetricData(string Name, object Value, string Unit);

/// <summary>
/// Represents trace data for console display.
/// </summary>
public record TraceData(
    string SpanId,
    string? ParentSpanId,
    string Name,
    DateTime StartTime,
    TimeSpan Duration,
    Dictionary<string, object> Attributes);

/// <summary>
/// Represents a simulation summary for console display.
/// </summary>
public record SimulationSummary(
    TimeSpan Duration,
    int EventsExecuted,
    int SpansCreated,
    int MetricsRecorded);
