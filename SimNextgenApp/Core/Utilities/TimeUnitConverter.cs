namespace SimNextgenApp.Core.Utilities;

/// <summary>
/// Provides utilities for converting between .NET TimeSpan values and simulation time units.
/// Enables simulations to run at any time scale (nanoseconds to years) while maintaining
/// integer precision for the simulation clock.
/// </summary>
public static class TimeUnitConverter
{
    /// <summary>
    /// Converts a TimeSpan duration to simulation time units using pure integer arithmetic.
    /// This is the primary method for scheduling events - it converts user-friendly TimeSpan
    /// values into the integer units used by the simulation clock.
    /// </summary>
    /// <param name="duration">The time duration to convert.</param>
    /// <param name="unit">The target simulation time unit.</param>
    /// <returns>The duration expressed as an integer count of simulation units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the unit is not supported.</exception>
    /// <remarks>
    /// Uses pure integer tick arithmetic (duration.Ticks / TicksPerUnit) to avoid floating-point
    /// precision loss and ensure deterministic, platform-independent conversions.
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
        // Use pure integer tick arithmetic to avoid floating-point precision loss
        // TimeSpan.Ticks = number of 100-nanosecond intervals
        return unit switch
        {
            SimulationTimeUnit.Ticks => duration.Ticks,
            SimulationTimeUnit.Microseconds => duration.Ticks / 10,  // 1 microsecond = 10 ticks
            SimulationTimeUnit.Milliseconds => duration.Ticks / TimeSpan.TicksPerMillisecond,
            SimulationTimeUnit.Seconds => duration.Ticks / TimeSpan.TicksPerSecond,
            SimulationTimeUnit.Minutes => duration.Ticks / TimeSpan.TicksPerMinute,
            SimulationTimeUnit.Hours => duration.Ticks / TimeSpan.TicksPerHour,
            SimulationTimeUnit.Days => duration.Ticks / TimeSpan.TicksPerDay,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), $"Unsupported simulation time unit: {unit}")
        };
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