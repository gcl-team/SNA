using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Context;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Events;
using SimNextgenApp.Exceptions;
using SimNextgenApp.Modeling;
using SimNextgenApp.Observability.Logs;
using SimNextgenApp.Observability.Exporters;

namespace SimNextgenApp.Core;

/// <summary>
/// Manages the execution of a discrete event simulation run.
/// Owns the simulation clock (tracked in simulation time units as defined by the SimulationProfile)
/// and the Future Event List (FEL), and processes events in chronological order.
/// </summary>
/// <remarks>
/// <para>
/// The simulation clock uses pure integer time representation. The meaning of one unit
/// (e.g., seconds, milliseconds, days) is determined by the SimulationProfile's TimeUnit setting.
/// This allows simulations to run at any time scale without being tied to a specific physical unit.
/// </para>
/// <para>
/// <strong>Threading Model:</strong> SimulationEngine is NOT thread-safe and must be used from a single thread.
/// All operations including Run(), Schedule(), and property access (HasFutureEvents, HeadEventTime)
/// must occur on the same thread. Concurrent access from multiple threads will result in undefined behavior.
/// This is consistent with discrete event simulation semantics where events must be processed sequentially
/// in chronological order.
/// </para>
/// </remarks>
public class SimulationEngine : IScheduler, IRunContext
{
    private readonly ILogger<SimulationEngine> _logger;
    private readonly ISimulationTracer? _tracer;

    private readonly SimulationProfile _profile;
    private readonly PriorityQueue<AbstractEvent, (long Time, long Sequence)> _fel;
    private long _clockTime = 0;
    private long _executedEventCount = 0;
    private long _eventSequenceCounter = 0;
    private bool _hasSimulationRun = false;
    private bool _warmupCompleteNotified = false; // Flag to ensure WarmedUp is called only once

    /// <summary>
    /// Gets the simulation model instance being executed by this engine.
    /// </summary>
    public ISimulationModel Model { get; }

    /// <summary>
    /// Gets the current simulation clock time as an integer count of simulation time units.
    /// The unit (seconds, milliseconds, etc.) is defined by the simulation profile's TimeUnit setting.
    /// </summary>
    public long ClockTime => _clockTime;

    public IScheduler Scheduler => this;

    /// <summary>
    /// Gets the current number of events that have been executed in the simulation.
    /// </summary>
    public long ExecutedEventCount => _executedEventCount;

    /// <summary>
    /// Gets a value indicating whether there are any events pending in the FEL.
    /// </summary>
    public bool HasFutureEvents => _fel.Count > 0;

    /// <summary>
    /// Gets the simulation time of the next scheduled event (in simulation time units),
    /// or long.MaxValue if no events are scheduled.
    /// </summary>
    public long HeadEventTime => _fel.TryPeek(out _, out var priority) ? priority.Time : long.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationEngine"/> class.
    /// </summary>
    /// <param name="profile">
    /// The simulation profile containing the model, run strategy, time unit configuration,
    /// and other simulation settings.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if profile is null.</exception>
    public SimulationEngine(SimulationProfile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));

        _logger = _profile.LoggerFactory?.CreateLogger<SimulationEngine>() ?? new NullLogger<SimulationEngine>();
        _tracer = _profile.Tracer;

        Model = _profile.Model;

        _fel = new PriorityQueue<AbstractEvent, (long Time, long Sequence)>();

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

        _clockTime = 0;
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
            using (LogContext.PushProperty("ProfileRunId", _profile.RunId))
            {
                while (strategy.ShouldContinue(this))
                {
                    if (!_fel.TryDequeue(out var currentEvent, out var priority))
                    {
                        break;
                    }

                    // currentEvent is guaranteed non-null here since TryDequeue succeeded


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

                    // 3. Execute Event
                    var traceId = currentEvent.EventId.ToString();
                    var spanId = Guid.NewGuid().ToString();
                    var startTime = DateTime.UtcNow;
                    using (LogContext.PushProperty("TraceId", traceId))
                    using (LogContext.PushProperty("SpanId", spanId))
                    using (LogContext.PushProperty("@Start", startTime))
                    {
                        var stopwatchLog = System.Diagnostics.Stopwatch.StartNew();

                        _tracer?.Trace(new TraceRecord(
                            Point: TracePoint.EventExecuting,
                            ClockTime: ClockTime,
                            EventId: currentEvent.EventId,
                            EventType: currentEvent.GetType().Name,
                            Details: currentEvent.GetTraceDetails()
                        ));

                        _logger.LogTrace("Executing event {EventType} at time {ExecutionTime}", currentEvent.GetType().Name, ClockTime);
                        currentEvent.Execute(this);

                        stopwatchLog.Stop();

                        using (LogContext.PushProperty("@Elapsed", stopwatch.Elapsed.TotalMilliseconds))
                        {
                            _logger.LogInformation(
                                "Executed event {EventType} (ID: {EventId})",
                                currentEvent.GetType().Name,
                                currentEvent.EventId);
                        }

                        _tracer?.Trace(new TraceRecord(
                            Point: TracePoint.EventCompleted,
                            ClockTime: ClockTime,
                            EventId: currentEvent.EventId,
                            EventType: currentEvent.GetType().Name,
                            Details: currentEvent.GetTraceDetails()
                        ));
                    }                    
                }
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
            ModelName: Model.Name,
            TimeUnit: _profile.TimeUnit
        );
    }

    /// <summary>
    /// Schedules a simulation event to occur at a specific absolute simulation time.
    /// </summary>
    /// <param name="ev">The event to schedule.</param>
    /// <param name="time">The absolute simulation time (in simulation time units) when the event should occur.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="ev"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="time"/> is before the current simulation clock time.</exception>
    /// <remarks>
    /// This method is NOT thread-safe. It must only be called from the same thread that invokes Run().
    /// Typically, this method is called from within event handlers during event execution.
    /// </remarks>
    public void Schedule(AbstractEvent ev, long time)
    {
        ArgumentNullException.ThrowIfNull(ev);

        if (time < ClockTime)
            throw new ArgumentOutOfRangeException(nameof(time), $"Cannot schedule event {ev.GetType().Name} at time {time} which is before current ClockTime {ClockTime}.");

        ev.ExecutionTime = time;

        var sequence = ++_eventSequenceCounter;
        _fel.Enqueue(ev, (time, sequence));

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
    /// <exception cref="OverflowException">Thrown if scheduling the event would exceed the maximum simulation time (long.MaxValue).</exception>
    /// <remarks>
    /// This method is NOT thread-safe. It must only be called from the same thread that invokes Run().
    /// Typically, this method is called from within event handlers during event execution.
    /// </remarks>
    public void Schedule(AbstractEvent ev, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(ev);

        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay cannot be negative.");

        // Convert TimeSpan delay to simulation units based on the profile's TimeUnit setting
        long delayInSimUnits = TimeUnitConverter.ConvertToSimulationUnits(delay, _profile.TimeUnit);

        // Use checked arithmetic to detect overflow when computing execution time
        long eventExecutionTime;
        try
        {
            eventExecutionTime = checked(ClockTime + delayInSimUnits);
        }
        catch (OverflowException)
        {
            throw new OverflowException(
                $"Cannot schedule event: ClockTime ({ClockTime}) + delay ({delayInSimUnits} {_profile.TimeUnit}) exceeds long.MaxValue. " +
                "Consider using a coarser time unit (e.g., Milliseconds instead of Ticks) or shorter simulation duration.");
        }

        _logger.LogDebug("Scheduling event {EventType} with delay {Delay} ({DelayInSimUnits} simulation units). FEL count before scheduling: {FELCount}",
            ev.GetType().Name, delay, delayInSimUnits, _fel.Count);

        Schedule(ev, eventExecutionTime);

        _logger.LogInformation("Scheduled event {EventType} for simulation time {ExecutionTime}. FEL count after scheduling: {FELCount}",
            ev.GetType().Name, eventExecutionTime, _fel.Count);
    }
}