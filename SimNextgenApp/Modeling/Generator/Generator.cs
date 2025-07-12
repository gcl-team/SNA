using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Events;

namespace SimNextgenApp.Modeling.Generator;

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
    /// <param name="engineContext">The simulation run context, used to get the current simulation time.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="engine"/> is null.</exception>
    public void ScheduleStartGenerating(IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(engineContext);

        engineContext.Scheduler.Schedule(new GeneratorStartEvent<TLoad>(this), engineContext.ClockTime);
    }

    /// <summary>
    /// Schedules an event to stop the load generation process.
    /// The stop will occur at the simulation time when the calling event is processed.
    /// Must be called after <see cref="Initialize"/> has been called.
    /// </summary>
    /// <param name="engine">The simulation engine instance, used to get current time for scheduling.</param>
    /// <exception cref="InvalidOperationException">Thrown if Initialize has not been called yet.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="engine"/> is null.</exception>
    public void ScheduleStopGenerating(IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(engineContext);

        engineContext.Scheduler.Schedule(new GeneratorStopEvent<TLoad>(this), engineContext.ClockTime);
    }

    /// <inheritdoc/>
    public override void Initialize(IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(engineContext);

        _logger.LogInformation("Generator '{GeneratorName}' (ID: {ModelId}) initializing. Scheduling start event at time 0.0.", Name, Id);

        engineContext.Scheduler.Schedule(new GeneratorStartEvent<TLoad>(this), 0.0);
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
    /// <param name="engineContext">The current run context (provides time and scheduler).</param>
    internal void HandleActivation(IRunContext engineContext)
    {
        if (!IsActive)
        {
            double currentTime = engineContext.ClockTime;
            _logger.LogDebug("Generator '{GeneratorName}' activating at time {ActivationTime}.", Name, currentTime);

            IsActive = true;
            StartTime = currentTime;
            LoadsGeneratedCount = 0;

            var random = RandomProvider;

            if (Configuration.IsSkippingFirst)
            {
                TimeSpan delay = Configuration.InterArrivalTime!(random);
                engineContext.Scheduler.Schedule(new GeneratorArriveEvent<TLoad>(this), currentTime + delay.TotalSeconds);
            }
            else
            {
                engineContext.Scheduler.Schedule(new GeneratorArriveEvent<TLoad>(this), currentTime);
            }
        }
    }

    /// <summary>
    /// Handles the deactivation of the generator by updating its state and logging relevant information.
    /// </summary>
    /// <param name="engineContext">The current run context (provides time and scheduler).</param>
    internal void HandleDeactivation(IRunContext engineContext)
    {
        if (IsActive)
        {
            IsActive = false;

            _logger.LogDebug("Generator '{GeneratorName}' deactivated at time {DeactivationTime}. Total loads generated: {LoadsGenerated}.", 
                Name, engineContext.ClockTime, LoadsGeneratedCount);
        }
    }

    /// <summary>
    /// Handles the generation of a new load and schedules the next load generation event.
    /// </summary>
    /// <param name="engineContext">The current run context (provides time and scheduler).</param>
    internal void HandleLoadGeneration(IRunContext engineContext)
    {
        if (IsActive)
        {
            double currentTime = engineContext.ClockTime;
            var random = RandomProvider;
            TLoad load = Configuration.LoadFactory!(random);
            TimeSpan nextDelay = Configuration.InterArrivalTime!(random);

            engineContext.Scheduler.Schedule(new GeneratorArriveEvent<TLoad>(this), nextDelay);

            LoadsGeneratedCount++;
            OnLoadGenerated(load, currentTime);
        }
    }

    void IOperatableGenerator<TLoad>.HandleActivation(IRunContext engineContext) => HandleActivation(engineContext);
    void IOperatableGenerator<TLoad>.HandleDeactivation(IRunContext engineContext) => HandleDeactivation(engineContext);
    void IOperatableGenerator<TLoad>.HandleLoadGeneration(IRunContext engineContext) => HandleLoadGeneration(engineContext);

    private void OnLoadGenerated(TLoad load, double time) => LoadGenerated?.Invoke(load, time);
}