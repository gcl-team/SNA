namespace SimNextgenApp.Core.Strategies;

/// <summary>
/// A run strategy that stops the simulation after a specified duration has elapsed.
/// The duration is expressed in simulation time units as defined by the SimulationProfile's TimeUnit setting.
/// </summary>
/// <remarks>
/// For example, if the profile's TimeUnit is Seconds and you specify runDuration=3600,
/// the simulation will run for 3600 seconds of simulated time.
/// </remarks>
public class DurationRunStrategy : IRunStrategy
{
    private readonly long _runDuration;

    /// <inheritdoc/>
    public long? WarmupEndTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DurationRunStrategy"/> class.
    /// </summary>
    /// <param name="runDuration">
    /// The total duration the simulation should run for, expressed in simulation time units
    /// (as defined by the SimulationProfile's TimeUnit setting). For example, if TimeUnit is Seconds,
    /// runDuration=3600 means the simulation runs for 3600 seconds of simulated time.
    /// </param>
    /// <param name="warmupDuration">
    /// Optional duration for the warm-up period, expressed in the same simulation time units as runDuration.
    /// If specified, ISimulationModel.WarmedUp will be called when ClockTime reaches this value.
    /// Must be less than runDuration if provided.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if runDuration is non-positive, or if warmupDuration is negative or greater than or equal to runDuration.</exception>
    public DurationRunStrategy(long runDuration, long? warmupDuration = null)
    {
        if (runDuration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runDuration), "Run duration must be positive.");
        }

        if (warmupDuration.HasValue)
        {
            if (warmupDuration.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupDuration), "Warm-up duration cannot be negative.");
            }
            if (warmupDuration.Value >= runDuration)
            {
                throw new ArgumentOutOfRangeException(nameof(warmupDuration), "Warm-up duration must be less than the total run duration.");
            }
            WarmupEndTime = warmupDuration.Value;
        }

        _runDuration = runDuration;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns true if the current ClockTime (in simulation units) is less than the specified duration.
    /// </remarks>
    public bool ShouldContinue(IRunContext runContext)
    {
        // Use < to stop exactly at or just after the duration is reached
        return runContext.ClockTime < _runDuration;
    }
}