using Microsoft.Extensions.Logging;
using SimNextgenApp.Modeling;

namespace SimNextgenApp.Core;

/// <summary>
/// Represents a configuration profile for executing a simulation run.
/// Encapsulates the simulation model, run strategy, time unit, logging options,
/// and run identity metadata.
/// </summary>
public class SimulationProfile(
    ISimulationModel model,
    IRunStrategy runStrategy,
    string? name = null,
    SimulationTimeUnit timeUnit = SimulationTimeUnit.Seconds,
    ILoggerFactory? loggerFactory = null,
    Guid? runId = null)
{
    /// <summary>
    /// The model to simulate.
    /// </summary>
    public ISimulationModel Model { get; } = model ?? throw new ArgumentNullException(nameof(model));

    /// <summary>
    /// The run strategy defining stopping conditions.
    /// </summary>
    public IRunStrategy RunStrategy { get; } = runStrategy ?? throw new ArgumentNullException(nameof(runStrategy));

    /// <summary>
    /// Defines the simulation time unit (e.g., Seconds, Minutes).
    /// </summary
    public SimulationTimeUnit TimeUnit { get; init; } = timeUnit;

    /// <summary>
    /// Optional logging factory.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; } = loggerFactory;

    /// <summary>
    /// Identifier to track this simulation profile.
    /// </summary>
    public Guid RunId { get; init; } = runId ?? Guid.NewGuid();

    /// <summary>
    /// Name of the simulation profile for easier debugging or reporting.
    /// </summary>
    public string Name => name ?? $"Profile {RunId}";
}

