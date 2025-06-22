using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SimNextgenApp.Events;
using SimNextgenApp.Exceptions;
using SimNextgenApp.Modeling;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Core;

/// <summary>
/// Manages the execution of a discrete event simulation run.
/// Owns the simulation clock and the Future Event List (FEL), and processes events in chronological order.
/// </summary>
public class SimulationEngine : IScheduler, IRunContext
{
    private readonly ILogger<SimulationEngine> _logger;
    private readonly ISimulationTracer? _tracer;

    private readonly SimulationProfile _profile;
    private readonly long _ticksPerSimulationUnit;
    private readonly PriorityQueue<AbstractEvent, (double Time, long Sequence)> _fel;
    private readonly Lock _felLock = new();
    private double _clockTime = 0.0;
    private long _executedEventCount = 0;
    private long _eventSequenceCounter = 0;
    private bool _hasSimulationRun = false;
    private bool _warmupCompleteNotified = false; // Flag to ensure WarmedUp is called only once

    /// <summary>
    /// Gets the simulation model instance being executed by this engine.
    /// </summary>
    public ISimulationModel Model { get; }

    /// <summary>
    /// Gets the current simulation clock time in simulation time units.
    /// </summary>
    public double ClockTime => _clockTime;

    public IScheduler Scheduler => this;

    /// <summary>
    /// Gets the current number of events that have been executed in the simulation.
    /// </summary>
    public long ExecutedEventCount => _executedEventCount;

    /// <summary>
    /// Gets a value indicating whether there are any events pending in the FEL.
    /// This property is thread-safe.
    /// </summary>
    public bool HasFutureEvents
    {
        get
        {
            lock (_felLock)
            {
                return _fel.Count > 0;
            }
        }
    }

    /// <summary>
    /// Gets the simulation time of the next scheduled event, or positive infinity if no events are scheduled.
    /// This property is thread-safe.
    /// </summary>
    public double HeadEventTime
    {
        get
        {
            lock (_felLock)
            {
                return _fel.TryPeek(out _, out var priority) ? priority.Time : double.PositiveInfinity;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationEngine"/> class.
    /// </summary>
    /// <param name="baseTimeUnit">Defines the meaning of one unit of the simulation 'double' ClockTime in terms of standard TimeSpan units.</param>
    /// <param name="model">The simulation model instance to execute.</param>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <exception cref="ArgumentNullException">Thrown if model or loggerFactory is null.</exception>
    public SimulationEngine(SimulationProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));

        _logger = _profile.LoggerFactory?.CreateLogger<SimulationEngine>() ?? new NullLogger<SimulationEngine>();
        _tracer = _profile.Tracer;

        _ticksPerSimulationUnit = TimeUnitConverter.GetTicksPerSimulationUnit(_profile.TimeUnit);
        Model = _profile.Model;

        _fel = new PriorityQueue<AbstractEvent, (double Time, long Sequence)>();

        _logger.LogInformation("SimulationEngine created for Model: {ModelName} (ID: {ModelId})", Model.Name, Model.Id);
    }

    /// <summary>
    /// Runs the simulation according to the specified strategy.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the engine is already running or initialization fails.</exception>
    /// <exception cref="SimulationException">Thrown if an error occurs during simulation execution.</exception>
    public SimulationResult Run()
    {
        if (_hasSimulationRun)
        {
            _logger.LogWarning("This SimulationEngine instance has already executed a simulation run and cannot be reused. Please create a new instance for each simulation.");
            throw new InvalidOperationException("This SimulationEngine instance has already executed a simulation run and cannot be reused.");
        }

        var strategy = _profile.RunStrategy;

        _logger.LogInformation("Starting simulation run for Model ID {ModelId} with strategy {StrategyName}...", Model.Id, strategy.GetType().Name);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _clockTime = 0.0;
        _executedEventCount = 0;
        _warmupCompleteNotified = false;

        try
        {
            _logger.LogDebug("Initializing model...");
            Model.Initialize(this);
            _hasSimulationRun = true;
            _logger.LogDebug("Model initialization complete.");
            _logger.LogInformation(">>> Starting main event loop. FEL count is: {FELCount}", _fel.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model initialization failed for Model ID {ModelId}.", Model.Id);
            stopwatch.Stop();
            throw new SimulationException($"Model initialization failed for Model ID {Model.Id}.", ex);
        }

        try
        {
            while (strategy.ShouldContinue(this) && _fel.TryDequeue(out var currentEvent, out var priority))
            {
                _executedEventCount += 1;

                // 1. Advance Clock
                if (priority.Time < ClockTime)
                {
                    // Should not happen with correct scheduling and FEL logic
                    throw new SimulationException($"FEL Error: Event {currentEvent.GetType().Name} time {priority.Time} is before ClockTime {ClockTime}. FEL is corrupted.");
                }

                _clockTime = priority.Time;

                // 2. Check for Warm-up Completion (only if applicable and not yet notified)
                if (!_warmupCompleteNotified && strategy.WarmupEndTime.HasValue && ClockTime >= strategy.WarmupEndTime.Value)
                {
                    if (Model is IWarmupAware warmupAwareModel)
                    {
                        _logger.LogInformation("Warm-up period complete at simulation time {WarmupEndTime}.", ClockTime);
                        warmupAwareModel.WarmedUp(ClockTime); // Notify model
                    }

                    _warmupCompleteNotified = true; // Ensure notification only happens once
                }

                _tracer?.Trace(new TraceRecord(
                    Point: TracePoint.EventExecuting,
                    ClockTime: ClockTime,
                    EventId: currentEvent.EventId,
                    EventType: currentEvent.GetType().Name,
                    Details: currentEvent.GetTraceDetails()
                ));

                // 3. Execute Event
                _logger.LogTrace("Executing event {EventType} at time {ExecutionTime}", currentEvent.GetType().Name, ClockTime);
                currentEvent.Execute(this);

                _tracer?.Trace(new TraceRecord(
                    Point: TracePoint.EventCompleted,
                    ClockTime: ClockTime,
                    EventId: currentEvent.EventId,
                    EventType: currentEvent.GetType().Name,
                    Details: currentEvent.GetTraceDetails()
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulation run failed during execution for Model ID {ModelId} at simulation time {ClockTime}.", Model.Id, ClockTime);
            stopwatch.Stop();
            throw new SimulationException($"Simulation run failed for Model ID {Model.Id}.", ex);
        }

        stopwatch.Stop();
        var realTimeDuration = stopwatch.Elapsed;

        _logger.LogInformation("Executed {Count} events during simulation.", ExecutedEventCount);
        _logger.LogInformation("Simulation run finished for Model ID {ModelId}. SimTime: {SimTime}, RealTime: {RealTime}ms", Model.Id, ClockTime, realTimeDuration.TotalMilliseconds);

        return new SimulationResult(
            ProfileRunId: _profile.RunId,
            ProfileName: _profile.Name,
            FinalClockTime: _clockTime,
            ExecutedEventCount: _executedEventCount,
            RealTimeDuration: realTimeDuration,
            ModelId: Model.Id,
            ModelName: Model.Name
        );
    }

    /// <summary>
    /// Schedules a simulation event to occur at a specific absolute simulation time.
    /// </summary>
    /// <param name="ev">The event to schedule.</param>
    /// <param name="time">The absolute simulation time (in simulation time units) when the event should occur.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="ev"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="time"/> is before the current simulation clock time.</exception>
    public void Schedule(AbstractEvent ev, double time)
    {
        ArgumentNullException.ThrowIfNull(ev);

        if (time < ClockTime)
            throw new ArgumentOutOfRangeException(nameof(time), $"Cannot schedule event {ev.GetType().Name} at time {time} which is before current ClockTime {ClockTime}.");

        ev.ExecutionTime = time;

        long sequence = Interlocked.Increment(ref _eventSequenceCounter);
        lock (_felLock)
        {
            _fel.Enqueue(ev, (time, sequence));
        }
        
        _logger.LogTrace("Scheduled event {EventType} for time {ExecutionTime} (Sequence {Sequence})", ev.GetType().Name, time, sequence);

        _tracer?.Trace(new TraceRecord(
            Point: TracePoint.EventScheduled,
            ClockTime: ClockTime,
            EventId: ev.EventId,
            EventType: ev.GetType().Name,
            Details: ev.GetTraceDetails()
        ));
    }

    /// <summary>
    /// Schedules a simulation event to occur after a specified delay relative to the current simulation clock time.
    /// </summary>
    /// <param name="ev">The event to schedule.</param>
    /// <param name="delay">The delay after which the event should execute, relative to the current simulation time.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="ev"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="delay"/> is negative.</exception>
    public void Schedule(AbstractEvent ev, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(ev);

        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");

        if (_ticksPerSimulationUnit <= 0)
            throw new InvalidOperationException("Ticks per simulation unit is not configured properly.");

        // Convert TimeSpan delay to simulation time units (double)
        double delayInSimUnits = (double)delay.Ticks / _ticksPerSimulationUnit;
        double eventExecutionTime = ClockTime + delayInSimUnits;

        _logger.LogDebug("Scheduling event {EventType} with delay {Delay} ({DelayInSimUnits} simulation units). FEL count before scheduling: {FELCount}",
            ev.GetType().Name, delay, delayInSimUnits, _fel.Count);

        Schedule(ev, eventExecutionTime);

        _logger.LogInformation("Scheduled event {EventType} for simulation time {ExecutionTime}. FEL count after scheduling: {FELCount}",
            ev.GetType().Name, eventExecutionTime, _fel.Count);
    }
}