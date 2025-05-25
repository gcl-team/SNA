namespace SimNextgenApp.Statistics // Assuming same namespace
{
    /// <summary>
    /// Tracks the total duration an entity spends in various discrete states over simulation time.
    /// </summary>
    /// <typeparam name="TState">The type of the state being tracked, should be an Enum.</typeparam>
    public class StateDurationMetric<TState> where TState : Enum
    {
        private List<(double Time, TState State)> _history;
        private Dictionary<TState, double> _stateDurationsInternal;

        /// <summary>
        /// The simulation time at which tracking started or was last reset (warmed up).
        /// </summary>
        public double InitialTime { get; private set; }

        /// <summary>
        /// The simulation time of the last recorded state transition.
        /// </summary>
        public double CurrentTime { get; private set; }

        /// <summary>
        /// The most recently recorded state of the entity.
        /// </summary>
        public TState CurrentState { get; private set; }

        /// <summary>
        /// Indicates whether a detailed history of state transitions is being recorded.
        /// </summary>
        public bool IsHistoryEnabled { get; private set; }

        /// <summary>
        /// A read-only dictionary containing the total active duration spent in each state.
        /// Key: State. Value: Total duration in that state.
        /// </summary>
        public IReadOnlyDictionary<TState, double> StateDurations => _stateDurationsInternal;

        /// <summary>
        /// A read-only list of (Time, State) tuples representing the history of state transitions,
        /// if history tracking is enabled. Otherwise, an empty list.
        /// </summary>
        public IReadOnlyList<(double Time, TState State)> History =>
            IsHistoryEnabled && _history != null
                ? _history.AsReadOnly()
                : Array.Empty<(double Time, TState State)>();

        /// <summary>
        /// Initializes a new instance of the <see cref="StateTracker{TState}"/> class.
        /// </summary>
        /// <param name="initialState">The initial state of the entity.</param>
        /// <param name="initialTime">The simulation time at which tracking begins. Defaults to 0.0.</param>
        /// <param name="enableHistory">True to record a history of all state transitions; otherwise, false. Defaults to false.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if initialTime is negative.</exception>
        public StateDurationMetric(TState initialState, double initialTime = 0.0, bool enableHistory = false)
        {
            if (initialTime < 0)
                throw new ArgumentOutOfRangeException(nameof(initialTime), "Initial time cannot be negative.");

            Init(initialState, initialTime, enableHistory);
        }

        private void Init(TState initialState, double time, bool enableHistory)
        {
            InitialTime = time;
            CurrentTime = time;
            CurrentState = initialState;
            IsHistoryEnabled = enableHistory;

            _stateDurationsInternal = [];
            // Initialize all possible enum states with 0 duration for completeness,
            // or only add them as they are encountered. For now, add as encountered.
            _history = [];
            if (IsHistoryEnabled)
            {
                // Record the initial state in history if enabled
                _history.Add((CurrentTime, CurrentState));
            }
        }

        /// <summary>
        /// Records a transition to a new state at the specified simulation time.
        /// </summary>
        /// <param name="newState">The state the entity is transitioning into.</param>
        /// <param name="clockTime">The simulation time of the state transition. Must not be less than CurrentTime.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if clockTime is less than CurrentTime.</exception>
        public void UpdateState(TState newState, double clockTime)
        {
            if (clockTime < CurrentTime)
            {
                throw new ArgumentOutOfRangeException(nameof(clockTime), $"New clock time ({clockTime}) cannot be less than current time ({CurrentTime}).");
            }

            if (clockTime > CurrentTime) // Only record duration if time has advanced
            {
                double durationInPreviousState = clockTime - CurrentTime;
                _stateDurationsInternal.TryGetValue(CurrentState, out double currentTotalDuration);
                _stateDurationsInternal[CurrentState] = currentTotalDuration + durationInPreviousState;
            }

            // Update current state and time
            CurrentTime = clockTime;
            CurrentState = newState;

            if (IsHistoryEnabled)
            {
                // Add new state to history, even if time hasn't advanced but state changed at same time instant
                _history.Add((CurrentTime, CurrentState));
            }
        }

        /// <summary>
        /// Resets the tracker, discarding all previously collected durations and history,
        /// and starts tracking from the current state at the specified simulation time.
        /// The IsHistoryEnabled setting is preserved.
        /// </summary>
        /// <param name="clockTime">The simulation time from which to start fresh tracking (warm-up time).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if clockTime is negative.</exception>
        public void WarmedUp(double clockTime)
        {
            if (clockTime < 0)
                throw new ArgumentOutOfRangeException(nameof(clockTime), "Warm-up time cannot be negative.");

            // CurrentState at the time of WarmedUp becomes the new initial state
            // The IsHistoryEnabled flag is preserved.
            Init(CurrentState, clockTime, IsHistoryEnabled);
        }

        /// <summary>
        /// Calculates the proportion of total observed time spent in a specific state,
        /// up to the specified 'asOfClockTime'.
        /// </summary>
        /// <param name="stateToQuery">The state for which to calculate the proportion.</param>
        /// <param name="asOfClockTime">The simulation time up to which the proportion is calculated.
        /// Must not be less than CurrentTime if calculating for the current ongoing state,
        /// and not less than InitialTime generally.</param>
        /// <returns>The proportion (0.0 to 1.0) of time spent in the specified state. Returns 0 if total observed time is zero.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if asOfClockTime is less than InitialTime,
        /// or if asOfClockTime is less than CurrentTime when querying the current active state.</exception>
        public double GetProportionOfTimeInState(TState stateToQuery, double asOfClockTime)
        {
            if (asOfClockTime < InitialTime)
                throw new ArgumentOutOfRangeException(nameof(asOfClockTime), $"As-of clock time ({asOfClockTime}) cannot be less than initial time ({InitialTime}).");

            double durationInQueriedState = 0;
            if (_stateDurationsInternal.TryGetValue(stateToQuery, out double recordedDuration))
            {
                durationInQueriedState = recordedDuration;
            }

            // If the state being queried is the current state, add the ongoing duration
            // since the last transition up to asOfClockTime.
            if (EqualityComparer<TState>.Default.Equals(stateToQuery, CurrentState))
            {
                if (asOfClockTime < CurrentTime)
                {
                    // This can happen if querying for a past time, but the state being queried IS the current state.
                    // This implies we are asking for proportion at a time before the last event.
                    // In this specific edge case, we should not add future duration.
                    // However, the overall total duration will also be up to 'asOfClockTime'.
                }
                else // asOfClockTime >= CurrentTime
                {
                     durationInQueriedState += asOfClockTime - CurrentTime;
                }
            }
            
            double totalObservedTimeSpan = asOfClockTime - InitialTime;
            if (totalObservedTimeSpan <= 0) // Use a small epsilon if strict zero is an issue with double comparisons
            {
                // If total time is zero (or negative, though prevented by check),
                // and the queried state is the initial state at initial time, proportion is 1. Else 0.
                return EqualityComparer<TState>.Default.Equals(stateToQuery, CurrentState) && asOfClockTime == InitialTime ? 1.0 : 0.0;
            }

            return durationInQueriedState / totalObservedTimeSpan;
        }

        /// <summary>
        /// Gets the total duration spent in a specific state up to the last recorded event (CurrentTime).
        /// </summary>
        /// <param name="stateToQuery">The state to get the duration for.</param>
        /// <returns>The total duration spent in the specified state.</returns>
        public double GetTotalDurationInState(TState stateToQuery)
        {
            _stateDurationsInternal.TryGetValue(stateToQuery, out double duration);
            // Note: This does NOT add ongoing duration for the CurrentState.
            // It reflects durations *completed* or recorded up to CurrentTime.
            // If you need "up to now including ongoing for current state", use GetProportionOfTimeInState's logic with CurrentTime.
            return duration;
        }
    }
}