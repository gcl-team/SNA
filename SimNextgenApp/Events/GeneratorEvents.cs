using SimNextgenApp.Core;
using SimNextgenApp.Modeling;

namespace SimNextgenApp.Events;

/// <summary>
/// Base for internal events specific to the Generator.
/// </summary>
internal abstract class AbstractGeneratorEvent<TLoad> : AbstractEvent where TLoad : notnull
{
    /// <summary>
    /// Gets the generator that owns the current instance.
    /// </summary>
    internal IGenerator<TLoad> OwningGenerator { get; }

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
internal sealed class GeneratorStartEvent<TLoad> : AbstractGeneratorEvent<TLoad> where TLoad : notnull
{
    public GeneratorStartEvent(Generator<TLoad> owner) : base(owner) { }

    public override void Execute(IRunContext engine)
    {
        if (OwningGenerator is IOperatableGenerator<TLoad> operatableGenerator)
        {
            operatableGenerator.HandleActivation(engine.ClockTime);
        }
        else
        {
            throw new InvalidOperationException($"The generator '{OwningGenerator.Name}' does not implement IOperatableGenerator and cannot handle this event.");
        }
    }

    public override string ToString() => $"{OwningGenerator.Name}_Start#{EventId} @ {ExecutionTime:F4}";
}

/// <summary>
/// Event that halts the load generation process for a specific Generator.
/// </summary>
internal sealed class GeneratorStopEvent<TLoad> : AbstractGeneratorEvent<TLoad> where TLoad : notnull
{
    public GeneratorStopEvent(Generator<TLoad> owner) : base(owner) { }

    public override void Execute(IRunContext engine)
    {
        if (OwningGenerator is IOperatableGenerator<TLoad> operatableGenerator)
        {
            operatableGenerator.HandleDeactivation();
        }
        else
        {
            throw new InvalidOperationException($"The generator '{OwningGenerator.Name}' does not implement IOperatableGenerator and cannot handle this event.");
        }
    }

    public override string ToString() => $"{OwningGenerator.Name}_Stop#{EventId} @ {ExecutionTime:F4}";
}

/// <summary>
/// Event representing the arrival (creation) of a new load by a specific Generator.
/// </summary>
internal sealed class GeneratorArriveEvent<TLoad> : AbstractGeneratorEvent<TLoad> where TLoad : notnull
{
    public GeneratorArriveEvent(Generator<TLoad> owner) : base(owner) { }

    public override void Execute(IRunContext engine)
    {
        if (OwningGenerator is IOperatableGenerator<TLoad> operatableGenerator)
        {
            operatableGenerator.HandleLoadGeneration(engine.ClockTime);
        }
        else
        {
            throw new InvalidOperationException($"The generator '{OwningGenerator.Name}' does not implement IOperatableGenerator and cannot handle this event.");
        }
    }

    public override string ToString() => $"{OwningGenerator.Name}_Arrive#{EventId} (Gen: {OwningGenerator.LoadsGeneratedCount}) @ {ExecutionTime:F4}";
}