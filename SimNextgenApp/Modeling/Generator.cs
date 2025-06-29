using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;

namespace SimNextgenApp.Modeling;

/// <summary>
/// Represents a source component in a simulation model that generates entities (loads) of type <typeparamref name="TLoad"/>.
/// It is typically used to model arrivals into a system, such as customers arriving at a store or data packets arriving at a router.
/// The generation is driven by a configured inter-arrival time distribution.
/// </summary
/// <typeparam name="TLoad">The type of load (entity) produced by this generator.</typeparam>
public class Generator<TLoad> : AbstractSimulationModel, IGenerator<TLoad>, IOperatableGenerator<TLoad> where TLoad : notnull
{
    private readonly GeneratorStaticConfig<TLoad> _config;
    private readonly Random _random;
    private IScheduler? _scheduler;
    private readonly ILogger<Generator<TLoad>> _logger;

    public double? StartTime { get; private set; }
    public bool IsActive { get; private set; }
    public int LoadsGeneratedCount { get; private set; }

    public event Action<TLoad, double>? LoadGenerated;

    /// <summary>
    /// Gets the configuration settings for the generator.
    /// </summary>
    internal GeneratorStaticConfig<TLoad> Configuration => _config;

    /// <summary>
    /// Gets the pseudo-random number generator used in this generator.
    /// </summary>
    internal Random RandomProvider => _random;

    /// <summary>
    /// Initialises a new instance of the <see cref="Generator{TLoad}"/> class.
    /// </summary>
    /// <param name="config">The static configuration settings for this generator.</param>
    /// <param name="seed">The seed for the random number stream used by this generator.</param>
    /// <param name="instanceName">A descriptive name for this generator instance (e.g., "CustomerArrivals").
    /// This name is used in logging and tracing output to uniquely identify this component.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is <c>null</c>.</exception>
    public Generator(GeneratorStaticConfig<TLoad> config, int seed, string instanceName, ILoggerFactory loggerFactory)
        : base(instanceName)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _random = new Random(seed);
        _logger = loggerFactory.CreateLogger<Generator<TLoad>>();

        IsActive = false;
        LoadsGeneratedCount = 0;

        _logger.LogInformation("Generator '{GeneratorName}' (ID: {ModelId}) created.", Name, Id);
    }

    /// <summary>
    /// Schedules the generator to start producing loads at the current simulation time.
    /// If the generator is already active, this action may be ignored.
    /// </summary>
    /// <param name="engine">The simulation run context, used to get the current simulation time.</param>
    /// <exception cref="InvalidOperationException">Thrown if Initialize has not been called yet.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="engine"/> is null.</exception>
    public void ScheduleStartGenerating(IRunContext engine)
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
    public void ScheduleStopGenerating(IRunContext engine)
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

        _logger.LogInformation("Generator '{GeneratorName}' (ID: {ModelId}) initializing. Scheduling start event at time 0.0.", Name, Id);

        _scheduler.Schedule(new GeneratorStartEvent<TLoad>(this), 0.0);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// For the generator, this resets the <see cref="LoadsGeneratedCount"/> to zero and sets the <see cref="LastActivationTime"/>.
    /// This ensures that statistics are only collected for the post-warm-up period.
    /// </remarks>
    public override void WarmedUp(double simulationTime)
    {
        StartTime = simulationTime;
        LoadsGeneratedCount = 0;
    }

    /// <summary>
    /// Activates the generator at the specified time, scheduling the first load arrival event if applicable.
    /// </summary>
    /// <remarks>If the generator is not already active, this method marks it as active, sets the activation
    /// time, and initialises the load generation count. Depending on the configuration, it schedules the first load
    /// arrival event either immediately or after a delay determined by the inter-arrival time function.</remarks>
    /// <param name="currentTime">The current simulation time, in seconds, at which the generator is being activated.</param>
    internal void HandleActivation(double currentTime)
    {
        if (!IsActive)
        {
            _logger.LogDebug("Generator '{GeneratorName}' activating at time {ActivationTime}.", Name, currentTime);

            IsActive = true;
            StartTime = currentTime;
            LoadsGeneratedCount = 0;

            var random = RandomProvider;

            if (Configuration.IsSkippingFirst)
            {
                TimeSpan delay = Configuration.InterArrivalTime!(random);
                _scheduler!.Schedule(new GeneratorArriveEvent<TLoad>(this), currentTime + delay.TotalSeconds);
            }
            else
            {
                _scheduler!.Schedule(new GeneratorArriveEvent<TLoad>(this), currentTime);
            }
        }
    }

    /// <summary>
    /// Handles the deactivation of the generator by updating its state and logging relevant information.
    /// </summary>
    internal void HandleDeactivation()
    {
        if (IsActive)
        {
            IsActive = false;

            _logger.LogDebug("Generator '{GeneratorName}' deactivated at time {DeactivationTime}. Total loads generated: {LoadsGenerated}.", 
                Name, _scheduler!.ClockTime, LoadsGeneratedCount);
        }
    }

    /// <summary>
    /// Handles the generation of a new load and schedules the next load generation event.
    /// </summary>
    /// <param name="currentTime">The current simulation time, in seconds, used to schedule the next load generation event.</param>
    internal void HandleLoadGeneration(double currentTime)
    {
        if (IsActive)
        {
            var random = RandomProvider;
            TLoad load = Configuration.LoadFactory!(random);
            TimeSpan nextDelay = Configuration.InterArrivalTime!(random);

            _scheduler!.Schedule(new GeneratorArriveEvent<TLoad>(this), currentTime + nextDelay.TotalSeconds);

            LoadsGeneratedCount++;
            OnLoadGenerated(load, currentTime);
        }
    }

    void IOperatableGenerator<TLoad>.HandleActivation(double currentTime) => HandleActivation(currentTime);
    void IOperatableGenerator<TLoad>.HandleDeactivation() => HandleDeactivation();
    void IOperatableGenerator<TLoad>.HandleLoadGeneration(double currentTime) => HandleLoadGeneration(currentTime);

    private void EnsureSchedulerInitialized()
    {
        if (_scheduler == null)
            throw new InvalidOperationException($"Generator '{Name}' has not been initialized with a scheduler. Call Initialize first.");
    }

    private void OnLoadGenerated(TLoad load, double time) => LoadGenerated?.Invoke(load, time);
}