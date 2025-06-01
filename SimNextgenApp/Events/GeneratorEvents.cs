using SimNextgenApp.Core;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SimNextgenApp.Tests")] // Allow unit tests to access internal members
namespace SimNextgenApp.Events;

/// <summary>
/// Base for internal events specific to the Generator.
/// </summary>
internal abstract class AbstractGeneratorEvent<TLoad> : AbstractEvent
{
    internal Generator<TLoad> OwningGenerator { get; }

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
        // OwningGenerator.HandleStopLogic(engine);
        if (OwningGenerator.IsActive) // Assuming IsActive has internal get
        {
            // OwningGenerator.IsActive = false; // Requires internal set
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
            // OwningGenerator.LoadsGeneratedCount++; // Requires internal set or method
            OwningGenerator.IncrementLoadsGenerated(); // New internal method

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