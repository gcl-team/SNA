namespace SimNextgenApp.Core.Strategies;

/// <summary>
/// A run strategy that stops the simulation based on a custom condition evaluated against the engine's state.
/// </summary>
public class ConditionalRunStrategy : IRunStrategy
{
    private readonly Func<IRunContext, bool> _continueCondition;

    /// <inheritdoc/>
    public double? WarmupEndTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionalRunStrategy"/> class.
    /// </summary>
    /// <param name="continueCondition">A function that takes the SimulationEngine state and returns true if the simulation should continue, false otherwise.</param>
    /// <param name="warmupEndTime">Optional absolute simulation time when the warm-up period ends.</param>
    /// <exception cref="ArgumentNullException">Thrown if continueCondition is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if warmupEndTime is negative.</exception>
    public ConditionalRunStrategy(Func<IRunContext, bool> continueCondition, double? warmupEndTime = null)
    {
        _continueCondition = continueCondition ?? throw new ArgumentNullException(nameof(continueCondition));

        if (warmupEndTime.HasValue && warmupEndTime.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warmupEndTime), "Warm-up end time cannot be negative.");
        }
        WarmupEndTime = warmupEndTime;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns the result of evaluating the provided continue condition function.
    /// </remarks>
    public bool ShouldContinue(IRunContext context)
    {
        return _continueCondition(context);
    }
}

// Usage Example:
// var strategy = new ConditionalRunStrategy(engine => engine.Model.GetComponent<MyQueue>("Q1").Length < 100);
// engine.Run(strategy);