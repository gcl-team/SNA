using SimNextgenApp.Core;
using SimNextgenApp.Modeling;

namespace SimNextgenApp.Events;

/// <summary>
/// Base for internal events specific to the Generator.
/// </summary>
internal abstract class AbstractGeneratorEvent<TLoad> : AbstractEvent
{
    internal Generator<TLoad> OwningGenerator { get; }

    /// <inheritdoc/>
    public override IDictionary<string, object>? GetTraceDetails()
    {
        return new Dictionary<string, object>
        {
            { "GeneratorName", OwningGenerator.Name },
            { "LoadsGeneratedSoFar", OwningGenerator.LoadsGeneratedCount }
        };
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="AbstractGeneratorEvent{TLoad}"/> class with the specified owning
    /// generator.
    /// </summary>
    /// <param name="owner">The generator that owns this event. Cannot be <see langword="null"/>.</param>
    protected AbstractGeneratorEvent(Generator<TLoad> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        OwningGenerator = owner;
    }
}

/// <summary>
/// Event that initiates the load generation process for a specific Generator.
/// </summary>
internal sealed class GeneratorStartEvent<TLoad> : AbstractGeneratorEvent<TLoad>
{
    public GeneratorStartEvent(Generator<TLoad> owner) : base(owner) { }

    public override void Execute(IRunContext engine)
    {
        double currentTime = engine.ClockTime;

        if (!OwningGenerator.IsActive)
        {
            OwningGenerator.PerformActivation(currentTime); // New internal method on Generator

            // Access config and random via internal properties on OwningGenerator
            var config = OwningGenerator.Configuration; // Assuming internal property Configuration
            var random = OwningGenerator.RandomProvider;  // Assuming internal property RandomProvider

            if (config.IsSkippingFirst)
            {
                TimeSpan delay = config.InterArrivalTime!(random);
                engine.Scheduler.Schedule(
                    new GeneratorArriveEvent<TLoad>(OwningGenerator),
                    currentTime + delay.TotalSeconds
                );
            }
            else
            {
                engine.Scheduler.Schedule(
                    new GeneratorArriveEvent<TLoad>(OwningGenerator),
                    currentTime
                );
            }
        }
    }

    public override string ToString() => $"{OwningGenerator.Name}_Start#{EventId} @ {ExecutionTime:F4}";
}

/// <summary>
/// Event that halts the load generation process for a specific Generator.
/// </summary>
internal sealed class GeneratorStopEvent<TLoad> : AbstractGeneratorEvent<TLoad>
{
    public GeneratorStopEvent(Generator<TLoad> owner) : base(owner) { }

    public override void Execute(IRunContext engine)
    {
        if (OwningGenerator.IsActive) // Assuming IsActive has internal get
        {
            OwningGenerator.PerformDeactivation(); // New internal method on Generator
        }
    }
    public override string ToString() => $"{OwningGenerator.Name}_Stop#{EventId} @ {ExecutionTime:F4}";
}

/// <summary>
/// Event representing the arrival (creation) of a new load by a specific Generator.
/// </summary>
internal sealed class GeneratorArriveEvent<TLoad> : AbstractGeneratorEvent<TLoad>
{
    public GeneratorArriveEvent(Generator<TLoad> owner) : base(owner) { }

    public override void Execute(IRunContext engine)
    {
        // OwningGenerator.HandleArriveLogic(engine);
        double currentTime = engine.ClockTime;

        if (OwningGenerator.IsActive)
        {
            var config = OwningGenerator.Configuration;
            var random = OwningGenerator.RandomProvider;

            TLoad load = config.LoadFactory!(random);
            OwningGenerator.IncrementLoadsGenerated();

            TimeSpan nextDelay = config.InterArrivalTime!(random);
            engine.Scheduler.Schedule(
                new GeneratorArriveEvent<TLoad>(OwningGenerator),
                currentTime + nextDelay.TotalSeconds
            );

            foreach (var action in OwningGenerator.LoadGeneratedActions)
            {
                action(load, currentTime);
            }
        }
    }
    public override string ToString() => $"{OwningGenerator.Name}_Arrive#{EventId} (Gen: {OwningGenerator.LoadsGeneratedCount}) @ {ExecutionTime:F4}";
}