using System;

namespace SimNextgenApp.Core;

/// <summary>
/// A run strategy that stops the simulation when the clock reaches a specific absolute time.
/// </summary>
public class AbsoluteTimeRunStrategy : IRunStrategy
{
    private readonly double _stopTime;

    /// <inheritdoc/>
    public double? WarmupEndTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AbsoluteTimeRunStrategy"/> class.
    /// </summary>
    /// <param name="stopTime">The absolute simulation clock time at which to stop (e.g., stop at time 5000.0).</param>
    /// <param name="warmupEndTime">Optional absolute simulation time when the warm-up period ends. Must be less than stopTime if provided.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if stopTime is non-positive, or if warmupEndTime is negative or greater than or equal to stopTime.</exception>
    public AbsoluteTimeRunStrategy(double stopTime, double? warmupEndTime = null)
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
    /// Returns true if the engine's current ClockTime is less than the specified stop time.
    /// </remarks>
    public bool ShouldContinue(SimulationEngine engine)
    {
        // Use < to stop exactly at or just after the stop time is reached
        return engine.ClockTime < _stopTime;
    }
}