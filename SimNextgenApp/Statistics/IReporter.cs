namespace SimNextgenApp.Statistics;

/// <summary>
/// Defines a contract for a component that generates and outputs a report.
/// </summary>
public interface IReporter
{
    /// <summary>
    /// Generates and outputs the report to its configured destination (e.g., logger, file, database).
    /// </summary>
    void Report();
}