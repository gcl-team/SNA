using SimNextgenApp.Modeling;
using SimNextgenApp.Exceptions;
using Microsoft.Extensions.Logging;

namespace SimNextgenApp.Core;

/// <summary>
/// Manages the execution of a discrete event simulation run.
/// Owns the simulation clock and the Future Event List (FEL),
/// and processes events in chronological order.
/// </summary>
public class SimulationEngine : IScheduler, IRunContext
{
    private readonly ILogger<SimulationEngine> _logger;
    private readonly long _ticksPerSimulationUnit;
    private readonly PriorityQueue<AbstractEvent, (double Time, long Sequence)> _fel;
    private long _eventSequenceCounter = 0;
    private bool _isInitialized = false;
    private bool _warmupCompleteNotified = false; // Flag to ensure WarmedUp is called only once

    /// <summary>
    /// Gets the simulation model instance being executed by this engine.
    /// </summary>
    public ISimulationModel Model { get; }

    /// <summary>
    /// Gets the current simulation clock time in simulation time units (e.g., seconds, minutes).
    /// </summary>
    public double ClockTime { get; private set; } = 0.0;

    public IScheduler Scheduler => this; // Engine is the scheduler

    /// <summary>
    /// Gets the current number of events that have been executed in the simulation.
    /// </summary>
    public long ExecutedEventCount { get; private set; } = 0;

    /// <summary>
    /// Gets a value indicating whether there are any events pending in the Future Event List.
    /// </summary>
    public bool HasFutureEvents => _fel.Count > 0;

    /// <summary>
    /// Gets the simulation time of the next scheduled event, or positive infinity if no events are scheduled.
    /// </summary>
    public double HeadEventTime => _fel.TryPeek(out _, out var priority) ? priority.Time : double.PositiveInfinity;

    // Property for real run time - maybe useful for performance profiling
    // Consider making its calculation optional.
    public TimeSpan? RealTimeDurationForLastRun { get; private set; }


    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationEngine"/> class.
    /// </summary>
    /// <param name="baseTimeUnit">Defines the meaning of one unit of the simulation 'double' ClockTime in terms of standard TimeSpan units.</param>
    /// <param name="model">The simulation model instance to execute.</param>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <exception cref="ArgumentNullException">Thrown if model or loggerFactory is null.</exception>
    public SimulationEngine(SimulationTimeUnit baseTimeUnit, ISimulationModel model, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _ticksPerSimulationUnit = TimeUnitConverter.GetTicksPerSimulationUnit(baseTimeUnit);

        Model = model ?? throw new ArgumentNullException(nameof(model));

        _logger = loggerFactory.CreateLogger<SimulationEngine>();

        _fel = new PriorityQueue<AbstractEvent, (double Time, long Sequence)>();

        _logger.LogInformation("SimulationEngine created for Model: {ModelName} (ID: {ModelId})", Model.Name, Model.Id);
    }

    /// <summary>
    /// Resets the engine to its initial state (clears FEL, resets clock).
    /// Should be called before starting a new run if reusing the engine instance.
    /// </summary>
    public void Reset()
    {
        ClockTime = 0.0;
        ExecutedEventCount = 0;
        _fel.Clear();
        _eventSequenceCounter = 0;
        _isInitialized = false;
        _warmupCompleteNotified = false;
        RealTimeDurationForLastRun = null;
        _logger.LogDebug("SimulationEngine reset.");
        // Note: This does NOT automatically reset the state within the Model itself.
        // Model state reset might need a separate mechanism if required between runs.
    }

    /// <summary>
    /// Runs the simulation according to the specified strategy.
    /// </summary>
    /// <param name="strategy">The strategy defining the run conditions (duration, event count, etc.) and potentially the warm-up period.</param>
    /// <exception cref="InvalidOperationException">Thrown if the engine is already running or initialization fails.</exception>
    /// <exception cref="SimulationException">Thrown if an error occurs during simulation execution.</exception>
    public void Run(IRunStrategy strategy)
    {
        _logger.LogInformation("Starting simulation run for Model ID {ModelId} with strategy {StrategyName}...", Model.Id, strategy.GetType().Name);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (!_isInitialized) // Ensure Reset is called or first run
        {
            Reset(); // Ensure clean state before initialization
            try
            {
                _logger.LogDebug("Initializing model...");
                Model.Initialize(this);
                _isInitialized = true;
                _logger.LogDebug("Model initialization complete.");
                _logger.LogInformation(">>> Starting main event loop. FEL count is: {FELCount}", _fel.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model initialization failed for Model ID {ModelId}.", Model.Id);
                stopwatch.Stop();
                throw new SimulationException($"Model initialization failed for Model ID {Model.Id}.", ex);
            }
        }
        else
        {
            // This could be an error if trying to re-run without reset, depending on desired behavior.
            _logger.LogWarning("Simulation engine is being re-run without an explicit Reset. Ensure this is intended.");
        }

        // --- Main Event Loop ---
        try
        {
            while (_fel.TryDequeue(out var currentEvent, out var priority) && strategy.ShouldContinue(this))
            {
                ExecutedEventCount += 1;

                // 1. Advance Clock
                if (priority.Time < ClockTime)
                {
                    // Should not happen with correct scheduling and FEL logic
                    throw new SimulationException($"FEL Error: Event {currentEvent.GetType().Name} time {priority.Time} is before ClockTime {ClockTime}. FEL is corrupted.");
                }

                ClockTime = priority.Time;

                // 2. Check for Warm-up Completion (only if applicable and not yet notified)
                if (!_warmupCompleteNotified && strategy.WarmupEndTime.HasValue && ClockTime >= strategy.WarmupEndTime.Value)
                {
                    _logger.LogInformation("Warm-up period complete at simulation time {WarmupEndTime}.", ClockTime);
                    Model.WarmedUp(ClockTime); // Notify model
                    _warmupCompleteNotified = true; // Ensure notification only happens once
                }

                // 3. Execute Event
                _logger.LogTrace("Executing event {EventType} at time {ExecutionTime}", currentEvent.GetType().Name, ClockTime);
                currentEvent.Execute(this); // Pass engine as context (or refine context passed)

                // 4. Check termination condition again (optional, allows events to trigger immediate stop)
                if (!strategy.ShouldContinue(this)) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation run failed during execution for Model ID {ModelId} at simulation time {ClockTime}.", Model.Id, ClockTime);
            stopwatch.Stop();
            throw new SimulationException($"Simulation run failed for Model ID {Model.Id}.", ex);
        }
        // --- End of Loop ---

        stopwatch.Stop();
        RealTimeDurationForLastRun = stopwatch.Elapsed;
        _logger.LogInformation("Simulation run finished for Model ID {ModelId}. SimTime: {SimTime}, RealTime: {RealTime}ms", Model.Id, ClockTime, RealTimeDurationForLastRun.Value.TotalMilliseconds);
    }


    // --- IScheduler Implementation ---

    /// <summary>
    /// Schedules a simulation event to occur at a specific future simulation time.
    /// </summary>
    /// <param name="ev">The event object to schedule.</param>
    /// <param name="time">The absolute simulation time (using simulation time units) at which the event should execute.</param>
    /// <exception cref="ArgumentNullException">Thrown if ev is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if time is before the current simulation ClockTime.</exception>
    public void Schedule(AbstractEvent ev, double time)
    {
        if (ev == null) throw new ArgumentNullException(nameof(ev));

        // Allow scheduling at current time, events will execute in FIFO order for same time due to sequence number.
        if (time < ClockTime)
        {
            throw new ArgumentOutOfRangeException(nameof(time), $"Event {ev.GetType().Name} cannot be scheduled at time {time} which is before current ClockTime {ClockTime}");
        }

        long sequence = Interlocked.Increment(ref _eventSequenceCounter);
        _fel.Enqueue(ev, (time, sequence));
        _logger.LogTrace("Scheduled event {EventType} for time {ExecutionTime} (Seq: {Sequence})", ev.GetType().Name, time, sequence);
    }
    
    public void Schedule(AbstractEvent ev, TimeSpan delay)
    {
        if (ev == null) 
            throw new ArgumentNullException(nameof(ev));
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");
        if (_ticksPerSimulationUnit <= 0)
            throw new InvalidOperationException("Ticks per simulation unit is not configured correctly.");

        _logger.LogDebug("Before scheduling {EventType}, FEL count is: {FELCount}", ev.GetType().Name, _fel.Count);

        // Convert TimeSpan delay to double simulation clock units
        double delayInSimUnits = (double)delay.Ticks / _ticksPerSimulationUnit;
        double eventExecutionTime = ClockTime + delayInSimUnits;
        Schedule(ev, eventExecutionTime);

        _logger.LogInformation("--> SCHEDULED: Event {EventType} for time {ExecutionTime}. FEL count is now: {FELCount}", ev.GetType().Name, ClockTime, _fel.Count);
    }
}