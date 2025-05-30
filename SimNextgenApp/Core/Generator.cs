using SimNextgenApp.Configurations;
using SimNextgenApp.Events;
using SimNextgenApp.Modeling;

namespace SimNextgenApp.Core;

/// <summary>
/// Represents a component that generates loads (entities) of type <typeparamref name="TLoad"/>
/// at specified intervals, based on its configuration.
/// This is a fundamental building block for discrete event simulation models.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) produced by this generator.</typeparam>
public class Generator<TLoad> : AbstractSimulationModel
{
    private readonly GeneratorStaticConfig<TLoad> _config;
    private readonly Random _random;
    private IScheduler? _scheduler;

    // Make config and random internally accessible for event classes
    internal GeneratorStaticConfig<TLoad> Configuration => _config;
    internal Random RandomProvider => _random;

    /// <summary>
    /// Gets the simulation time when the generator was last started or when the warm-up period ended.
    /// This value is set when the generator becomes active or after <see cref="WarmedUp"/>.
    /// Will be <c>null</c> if the generator has not yet started or warmed up.
    /// The unit of time is consistent with the simulation engine's clock.
    /// </summary>
    public double? StartTime { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the generator is currently active and producing loads.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the total number of loads generated since the last start or warm-up.
    /// </summary>
    public int LoadsGeneratedCount { get; private set; }

    /// <summary>
    /// Gets a list of actions to be executed when a new load is generated.
    /// Each function in this list will be invoked with the newly created load.
    /// These actions are responsible for any subsequent scheduling related to the load.
    /// </summary>
    public List<Action<TLoad, double>> LoadGeneratedActions { get; }

    /// <summary>
    /// Initialises a new instance of the <see cref="Generator{TLoad}"/> class.
    /// </summary>
    /// <param name="config">The static configuration settings for this generator.</param>
    /// <param name="seed">The seed for the random number stream used by this generator.</param>
    /// <param name="instanceName">A unique name for this generator instance (e.g., "CustomerArrivals").
    /// This will be used as the base name for the simulation model.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is <c>null</c>.</exception>
    public Generator(GeneratorStaticConfig<TLoad> config, int seed, string instanceName)
        : base(instanceName)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _random = new Random(seed);

        IsActive = false;
        LoadsGeneratedCount = 0;
        LoadGeneratedActions = [];
    }

    /// <summary>
    /// Schedules an event to start the load generation process.
    /// The start will occur at the simulation time when the calling event is processed.
    /// Must be called after <see cref="Initialize"/> has been called.
    /// </summary>
    /// <param name="engine">The simulation engine instance, used to get current time for scheduling.</param>
    /// <exception cref="InvalidOperationException">Thrown if Initialize has not been called yet.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="engine"/> is null.</exception>
    public void StartGenerating(IRunContext engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        EnsureSchedulerInitialized();
        _scheduler!.Schedule(new GeneratorStartEvent<TLoad>(this), engine.ClockTime);
    }

    /// <summary>
    /// Schedules an event to stop the load generation process.
    /// The stop will occur at the simulation time when the calling event is processed.
    /// Must be called after <see cref="Initialize"/> has been called.
    /// </summary>
    /// <param name="engine">The simulation engine instance, used to get current time for scheduling.</param>
    /// <exception cref="InvalidOperationException">Thrown if Initialize has not been called yet.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="engine"/> is null.</exception>
    public void StopGenerating(IRunContext engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        EnsureSchedulerInitialized();
        _scheduler!.Schedule(new GeneratorStopEvent<TLoad>(this), engine.ClockTime);
    }

    /// <inheritdoc/>
    public override void Initialize(IScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        _scheduler = scheduler;
    }

    /// <inheritdoc/>
    public override void WarmedUp(double simulationTime)
    {
        // 'simulationTime' is the time WarmedUp is called by the engine.
        StartTime = simulationTime;
        LoadsGeneratedCount = 0;
    }

    private void EnsureSchedulerInitialized()
    {
        if (_scheduler == null)
            throw new InvalidOperationException($"Generator '{Name}' has not been initialized with a scheduler. Call Initialize first.");
    }

    internal void PerformActivation(double currentTime)
    {
        IsActive = true;
        StartTime = currentTime;
        LoadsGeneratedCount = 0;
    }

    internal void PerformDeactivation()
    {
        IsActive = false;
    }

    internal void IncrementLoadsGenerated()
    {
        LoadsGeneratedCount++;
    }
}