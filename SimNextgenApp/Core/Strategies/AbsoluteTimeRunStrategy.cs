namespace SimNextgenApp.Core.Strategies;

/// <summary>
/// A run strategy that stops the simulation when the clock reaches a specific absolute time.
/// The time value is expressed in simulation time units as defined by the SimulationProfile's TimeUnit setting.
/// </summary>
/// <remarks>
/// For example, if the profile's TimeUnit is Seconds and you specify stopTime=86400,
/// the simulation will stop when the clock reaches 86400 seconds (1 day of simulated time).
/// </remarks>
public class AbsoluteTimeRunStrategy : IRunStrategy
{
    private readonly long _stopTime;

    /// <inheritdoc/>
    public long? WarmupEndTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteTimeRunStrategy"/> class.
    /// </summary>
    /// <param name="stopTime">
    /// The absolute simulation clock time at which to stop, expressed in simulation time units
    /// (as defined by the SimulationProfile's TimeUnit setting). For example, if TimeUnit is Seconds,
    /// stopTime=86400 means the simulation stops at 86400 seconds of simulated time.
    /// </param>
    /// <param name="warmupEndTime">
    /// Optional absolute simulation time when the warm-up period ends, expressed in the same simulation time units.
    /// Must be less than stopTime if provided.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if stopTime is non-positive, or if warmupEndTime is negative or greater than or equal to stopTime.</exception>
    public AbsoluteTimeRunStrategy(long stopTime, long? warmupEndTime = null)
    {
        if (stopTime <= 0) // Assuming simulation time must be positive for a stop time
        {
            throw new ArgumentOutOfRangeException(nameof(stopTime), "Stop time must be positive.");
        }

        if (warmupEndTime.HasValue)
        {
            if (warmupEndTime.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupEndTime), "Warm-up end time cannot be negative.");
            }
            if (warmupEndTime.Value >= stopTime)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupEndTime), "Warm-up end time must be less than the stop time.");
            }
            WarmupEndTime = warmupEndTime.Value;
        }

        _stopTime = stopTime;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns true if the current ClockTime (in simulation units) is less than the specified stop time.
    /// </remarks>
    public bool ShouldContinue(IRunContext context)
    {
        // Use < to stop exactly at or just after the stop time is reached
        return context.ClockTime < _stopTime;
    }
}