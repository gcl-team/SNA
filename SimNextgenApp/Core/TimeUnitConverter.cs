namespace SimNextgenApp.Core;

public static class TimeUnitConverter
{
    public static long GetTicksPerSimulationUnit(SimulationTimeUnit unit)
    {
        return unit switch
        {
            SimulationTimeUnit.Ticks => 1,
            SimulationTimeUnit.Microseconds => TimeSpan.TicksPerMicrosecond,
            SimulationTimeUnit.Milliseconds => TimeSpan.TicksPerMillisecond,
            SimulationTimeUnit.Seconds => TimeSpan.TicksPerSecond,
            SimulationTimeUnit.Minutes => TimeSpan.TicksPerMinute,
            SimulationTimeUnit.Hours => TimeSpan.TicksPerHour,
            SimulationTimeUnit.Days => TimeSpan.TicksPerDay,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), $"Unsupported simulation time unit: {unit}")
        };
    }
}