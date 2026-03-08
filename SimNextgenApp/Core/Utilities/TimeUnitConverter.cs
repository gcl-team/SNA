namespace SimNextgenApp.Core.Utilities;

/// <summary>
/// Provides utilities for converting between .NET TimeSpan values and simulation time units.
/// Enables simulations to run at any time scale (nanoseconds to years) while maintaining
/// integer precision for the simulation clock.
/// </summary>
public static class TimeUnitConverter
{
    /// <summary>
    /// Converts a TimeSpan duration to simulation time units.
    /// This is the primary method for scheduling events - it converts user-friendly TimeSpan
    /// values into the integer units used by the simulation clock.
    /// </summary>
    /// <param name="duration">The time duration to convert.</param>
    /// <param name="unit">The target simulation time unit.</param>
    /// <returns>The duration expressed as an integer count of simulation units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the unit is not supported.</exception>
    /// <remarks>
    /// Fractional parts are truncated to integers. For sub-unit precision, use a finer time unit.
    /// For example, if you need millisecond precision but are using Seconds, switch to Milliseconds.
    /// </remarks>
    /// <example>
    /// // Millisecond simulation:
    /// long delay = ConvertToSimulationUnits(TimeSpan.FromSeconds(5.5), SimulationTimeUnit.Milliseconds);
    /// // Result: 5500 (milliseconds)
    /// </example>
    public static long ConvertToSimulationUnits(TimeSpan duration, SimulationTimeUnit unit)
    {
        return unit switch
        {
            SimulationTimeUnit.Ticks => duration.Ticks,
            SimulationTimeUnit.Microseconds => (long)duration.TotalMicroseconds,
            SimulationTimeUnit.Milliseconds => (long)duration.TotalMilliseconds,
            SimulationTimeUnit.Seconds => (long)duration.TotalSeconds,
            SimulationTimeUnit.Minutes => (long)duration.TotalMinutes,
            SimulationTimeUnit.Hours => (long)duration.TotalHours,
            SimulationTimeUnit.Days => (long)duration.TotalDays,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), $"Unsupported simulation time unit: {unit}")
        };
    }

    /// <summary>
    /// Converts simulation time units back to a fractional value in the same units.
    /// Primarily used for display purposes to show human-readable time values.
    /// </summary>
    /// <param name="simulationUnits">The simulation clock time in integer units.</param>
    /// <param name="unit">The simulation time unit (kept for API clarity, though not used in conversion).</param>
    /// <returns>The time value as a double in the same units (for display formatting).</returns>
    /// <remarks>
    /// Since simulation time is already in the specified unit, this just converts long to double.
    /// The real value is in making it explicit what unit the value represents.
    /// </remarks>
    /// <example>
    /// // If ClockTime = 5500 milliseconds:
    /// double time = ConvertFromSimulationUnits(5500, SimulationTimeUnit.Milliseconds);
    /// // Result: 5500.0 (milliseconds)
    ///
    /// // For display: $"{time:F2} {GetUnitDisplayName(unit)}" → "5500.00 ms"
    /// </example>
    public static double ConvertFromSimulationUnits(long simulationUnits, SimulationTimeUnit unit)
    {
        // Simulation units are already in the target unit, just return as double for formatting
        _ = unit; // Suppress unused parameter warning - kept for API clarity
        return simulationUnits;
    }

    /// <summary>
    /// Gets a human-readable display name for a simulation time unit.
    /// </summary>
    /// <param name="unit">The simulation time unit.</param>
    /// <returns>A full name suitable for display (e.g., "seconds", "milliseconds").</returns>
    public static string GetUnitDisplayName(SimulationTimeUnit unit)
    {
        return unit switch
        {
            SimulationTimeUnit.Ticks => "ticks",
            SimulationTimeUnit.Microseconds => "microseconds",
            SimulationTimeUnit.Milliseconds => "milliseconds",
            SimulationTimeUnit.Seconds => "seconds",
            SimulationTimeUnit.Minutes => "minutes",
            SimulationTimeUnit.Hours => "hours",
            SimulationTimeUnit.Days => "days",
            _ => unit.ToString().ToLowerInvariant()
        };
    }

    /// <summary>
    /// Gets a short symbol for a simulation time unit.
    /// </summary>
    /// <param name="unit">The simulation time unit.</param>
    /// <returns>A short symbol suitable for compact display (e.g., "s", "ms", "μs").</returns>
    public static string GetUnitSymbol(SimulationTimeUnit unit)
    {
        return unit switch
        {
            SimulationTimeUnit.Ticks => "ticks",
            SimulationTimeUnit.Microseconds => "μs",
            SimulationTimeUnit.Milliseconds => "ms",
            SimulationTimeUnit.Seconds => "s",
            SimulationTimeUnit.Minutes => "min",
            SimulationTimeUnit.Hours => "hr",
            SimulationTimeUnit.Days => "days",
            _ => unit.ToString().ToLowerInvariant()
        };
    }
}