using System;

namespace SimNextgenApp.Core;

/// <summary>
/// A run strategy that stops the simulation after a specified duration has elapsed.
/// </summary>
public class DurationRunStrategy : IRunStrategy
{
    private readonly double _runDuration;

    /// <inheritdoc/>
    public double? WarmupEndTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DurationRunStrategy"/> class.
    /// </summary>
    /// <param name="runDuration">The total duration the simulation should run for (e.g., 1000.0 time units).</param>
    /// <param name="warmupDuration">Optional duration for the warm-up period. If specified, ISimulationModel.WarmedUp
    /// will be called when ClockTime reaches this value. Must be less than runDuration if provided.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if runDuration is non-positive, or if warmupDuration is negative or greater than or equal to runDuration.</exception>
    public DurationRunStrategy(double runDuration, double? warmupDuration = null)
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
    /// Returns true if the engine's current ClockTime is less than the specified run duration.
    /// </remarks>
    public bool ShouldContinue(SimulationEngine engine)
    {
        // Use < to stop exactly at or just after the duration is reached
        return engine.ClockTime < _runDuration;
    }
}