using System;

namespace SimNextgenApp.Core;

/// <summary>
/// A run strategy that stops the simulation after a specific number of events have been executed.
/// </summary>
public class EventCountRunStrategy : IRunStrategy
{
    private readonly long _maxEventCount;

    /// <inheritdoc/>
    public double? WarmupEndTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventCountRunStrategy"/> class.
    /// </summary>
    /// <param name="maxEventCount">The total number of simulation events to execute before stopping.</param>
    /// <param name="warmupEndTime">Optional absolute simulation time when the warm-up period ends.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if maxEventCount is non-positive, or if warmupEndTime is negative.</exception>
    public EventCountRunStrategy(long maxEventCount, double? warmupEndTime = null)
    {
        if (maxEventCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEventCount), "Maximum event count must be positive.");
        }

        if (warmupEndTime.HasValue && warmupEndTime.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupEndTime), "Warm-up end time cannot be negative.");
        }

        _maxEventCount = maxEventCount;
        WarmupEndTime = warmupEndTime;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns true if the engine's executed event count is less than the specified maximum.
    /// NOTE: Requires the SimulationEngine to track the number of executed events.
    /// </remarks>
    public bool ShouldContinue(SimulationEngine engine)
    {
        return engine.ExecutedEventCount < _maxEventCount;
    }
}