namespace SimNextgenApp.Core.Utilities;

/// <summary>
/// The physical duration that one unit of the simulation clock represents.
/// </summary>
/// <remarks>
/// The simulation clock counts whole units, so <see cref="System.TimeSpan"/> delays are converted to this unit
/// and any fraction of a unit is truncated. Choose a unit fine enough for the shortest delays in the model.
/// </remarks>
public enum SimulationTimeUnit
{
    /// <summary>One unit is one <see cref="System.TimeSpan"/> tick (100 nanoseconds).</summary>
    Ticks,
    /// <summary>One unit is one microsecond.</summary>
    Microseconds,
    /// <summary>One unit is one millisecond.</summary>
    Milliseconds,
    /// <summary>One unit is one second.</summary>
    Seconds,
    /// <summary>One unit is one minute.</summary>
    Minutes,
    /// <summary>One unit is one hour.</summary>
    Hours,
    /// <summary>One unit is one day.</summary>
    Days
}