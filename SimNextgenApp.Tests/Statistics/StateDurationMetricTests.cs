using SimNextgenApp.Statistics; 

namespace SimNextgenApp.Tests.Statistics;

// Define a sample Enum for testing
public enum TestMachineState
{
    Idle,
    Busy,
    Maintenance
}

public class StateDurationMetricTests
{
    [Fact(DisplayName = "Constructor with default parameters should initialise the metric correctly.")]
    public void Constructor_DefaultInitialTime_InitializesCorrectly()
    {
        // Arrange
        var initialState = TestMachineState.Idle;

        // Act
        var metric = new StateDurationMetric<TestMachineState>(initialState);

        // Assert
        AssertHelpers.AreEqual(0.0, metric.InitialTime);
        AssertHelpers.AreEqual(0.0, metric.CurrentTime);
        Assert.Equal(initialState, metric.CurrentState);
        Assert.False(metric.IsHistoryEnabled);
        Assert.NotNull(metric.StateDurations);
        Assert.Empty(metric.StateDurations); // No durations accumulated yet for any specific state
        Assert.NotNull(metric.History);
        Assert.Empty(metric.History);
    }

    [Fact(DisplayName = "Constructor with specified initial time and history enabled should initialise correctly.")]
    public void Constructor_WithInitialTimeAndHistoryEnabled_InitializesCorrectly()
    {
        // Arrange
        var initialState = TestMachineState.Busy;
        var initialTime = 10.0;
        var enableHistory = true;

        // Act
        var metric = new StateDurationMetric<TestMachineState>(initialState, initialTime, enableHistory);

        // Assert
        AssertHelpers.AreEqual(initialTime, metric.InitialTime);
        AssertHelpers.AreEqual(initialTime, metric.CurrentTime);
        Assert.Equal(initialState, metric.CurrentState);
        Assert.True(metric.IsHistoryEnabled);
        Assert.NotNull(metric.StateDurations);
        Assert.Empty(metric.StateDurations);
        Assert.NotNull(metric.History);
        Assert.Single(metric.History); // Initial state is recorded in history
        AssertHelpers.AreEqual(initialTime, metric.History[0].Time);
        Assert.Equal(initialState, metric.History[0].State);
    }

    [Fact(DisplayName = "Constructor should throw ArgumentOutOfRangeException for a negative initial time.")]
    public void Constructor_NegativeInitialTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var initialState = TestMachineState.Idle;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            new StateDurationMetric<TestMachineState>(initialState, -1.0)
        );
    }

    [Fact(DisplayName = "UpdateState with a single transition should record duration for the previous state.")]
    public void UpdateState_SingleTransition_RecordsDurationAndUpdatesState()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);
        var newState = TestMachineState.Busy;
        var clockTime = 10.0;

        // Act
        metric.UpdateState(newState, clockTime);

        // Assert
        AssertHelpers.AreEqual(clockTime, metric.CurrentTime);
        Assert.Equal(newState, metric.CurrentState);
        Assert.True(metric.StateDurations.ContainsKey(TestMachineState.Idle));
        AssertHelpers.AreEqual(10.0, metric.StateDurations[TestMachineState.Idle]); // 10s in Idle
        Assert.False(metric.StateDurations.ContainsKey(TestMachineState.Busy)); // No completed duration for Busy yet
    }

    [Fact(DisplayName = "UpdateState with multiple transitions should accumulate durations correctly.")]
    public void UpdateState_MultipleTransitions_AccumulatesDurationsCorrectly()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);

        // Act
        metric.UpdateState(TestMachineState.Busy, 10.0);        // Idle for 10s
        metric.UpdateState(TestMachineState.Maintenance, 15.0); // Busy for 5s
        metric.UpdateState(TestMachineState.Idle, 25.0);        // Maintenance for 10s

        // Assert
        AssertHelpers.AreEqual(25.0, metric.CurrentTime);
        Assert.Equal(TestMachineState.Idle, metric.CurrentState);

        AssertHelpers.AreEqual(10.0, metric.StateDurations[TestMachineState.Idle]);
        AssertHelpers.AreEqual(5.0, metric.StateDurations[TestMachineState.Busy]);
        AssertHelpers.AreEqual(10.0, metric.StateDurations[TestMachineState.Maintenance]);
    }

    [Fact(DisplayName = "\"UpdateState with a self-transition should record the duration spent in that state.\"")]
    public void UpdateState_TransitionToSameStateAtLaterTime_AddsToDuration()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);

        // Act
        // This scenario is a bit unusual for state transitions, but the logic should handle it.
        // This simulates an event that re-affirms the current state at a later time.
        // The metric should correctly record the time spent in 'Idle' before this call.
        metric.UpdateState(TestMachineState.Idle, 5.0); // Idle for 5s

        // Assert
        AssertHelpers.AreEqual(5.0, metric.CurrentTime);
        Assert.Equal(TestMachineState.Idle, metric.CurrentState);
        Assert.True(metric.StateDurations.ContainsKey(TestMachineState.Idle));
        AssertHelpers.AreEqual(5.0, metric.StateDurations[TestMachineState.Idle]); // Verify that the 5 seconds spent in Idle was correctly recorded.
    }


    [Fact(DisplayName = "UpdateState with a zero-duration transition should update state without accumulating time.")]
    public void UpdateState_TransitionAtSameTime_UpdatesStateNoDurationChange()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 5.0);

        // Act
        metric.UpdateState(TestMachineState.Busy, 5.0); // Transition at the exact same time

        // Assert
        AssertHelpers.AreEqual(5.0, metric.CurrentTime);
        Assert.Equal(TestMachineState.Busy, metric.CurrentState);
        Assert.False(metric.StateDurations.ContainsKey(TestMachineState.Idle)); // No duration for Idle as time did not advance
    }

    [Fact(DisplayName = "UpdateState should throw ArgumentOutOfRangeException if time moves backward.")]
    public void UpdateState_ClockTimeGoesBackwards_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 10.0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            metric.UpdateState(TestMachineState.Busy, 5.0)
        );
    }

    [Fact(DisplayName = "UpdateState should add all transitions to history when enabled.")]
    public void UpdateState_WithHistoryEnabled_RecordsHistory()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0, enableHistory: true);

        // Act
        metric.UpdateState(TestMachineState.Busy, 10.0);
        metric.UpdateState(TestMachineState.Maintenance, 15.0);

        // Assert
        Assert.Equal(3, metric.History.Count); // Initial + 2 updates
        Assert.Equal((0.0, TestMachineState.Idle), metric.History[0]);
        Assert.Equal((10.0, TestMachineState.Busy), metric.History[1]);
        Assert.Equal((15.0, TestMachineState.Maintenance), metric.History[2]);
    }

    [Fact(DisplayName = "WarmedUp should reset all statistics and re-initialise the metric's state.")]
    public void WarmedUp_ResetsMetricsAndHistoryCorrectly()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0, enableHistory: true);
        metric.UpdateState(TestMachineState.Busy, 10.0); // Idle for 10s
        var warmUpTime = 15.0;
        var stateAtWarmUp = TestMachineState.Busy; // This is CurrentState before WarmedUp

        // Act
        metric.WarmedUp(warmUpTime);

        // Assert
        AssertHelpers.AreEqual(warmUpTime, metric.InitialTime);
        AssertHelpers.AreEqual(warmUpTime, metric.CurrentTime);
        Assert.Equal(stateAtWarmUp, metric.CurrentState); // State at warm-up time is the new initial
        Assert.True(metric.IsHistoryEnabled); // Preserved
        Assert.Empty(metric.StateDurations);  // Cleared

        Assert.Single(metric.History); // History reset with current state at warm-up time
        Assert.Equal((warmUpTime, stateAtWarmUp), metric.History[0]);
    }

    [Fact(DisplayName = "WarmedUp should throw ArgumentOutOfRangeException for a negative warm-up time.")]
    public void WarmedUp_NegativeClockTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.WarmedUp(-5.0));
    }

    [Fact(DisplayName = "GetTotalDurationInState should return zero for a state that was never entered.")]
    public void GetTotalDurationInState_StateNeverEntered_ReturnsZero()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);
        metric.UpdateState(TestMachineState.Busy, 10.0);

        // Act
        var duration = metric.GetTotalDurationInState(TestMachineState.Maintenance);

        // Assert
        AssertHelpers.AreEqual(0.0, duration);
    }

    [Fact(DisplayName = "GetTotalDurationInState should return the correct total recorded duration.")]
    public void GetTotalDurationInState_ForVariousStates_ReturnsCorrectDurations()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);
        metric.UpdateState(TestMachineState.Busy, 10.0);  // Idle: 10s
        metric.UpdateState(TestMachineState.Idle, 15.0);  // Busy: 5s
        metric.UpdateState(TestMachineState.Busy, 25.0);  // Idle: 10s (total Idle = 10+10=20)

        // Act
        var idleDuration = metric.GetTotalDurationInState(TestMachineState.Idle);
        var busyDuration = metric.GetTotalDurationInState(TestMachineState.Busy);

        // Assert
        AssertHelpers.AreEqual(20.0, idleDuration);
        AssertHelpers.AreEqual(5.0, busyDuration);
    }

    [Fact(DisplayName = "GetProportionOfTimeInState returns correct proportions for each state at a given asOfTime.")]
    public void GetProportionOfTimeInState_BasicScenario_CorrectProportion()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0); // InitialTime = 0
        metric.UpdateState(TestMachineState.Busy, 10.0);                                    // Idle for 10s
        metric.UpdateState(TestMachineState.Maintenance, 15.0);                             // Busy for 5s
        // CurrentState = Maintenance, CurrentTime = 15.0
        // StateDurations: { Idle: 10, Busy: 5 }

        var asOfTime = 20.0;

        // Act
        // Proportion of Maintenance = (ongoing Maintenance duration from 15 to 20) / (total time 20-0)
        // Ongoing Maintenance = 20 - 15 = 5s
        // Total Time = 20s
        // Proportion = 5 / 20 = 0.25
        var propMaintenance = metric.GetProportionOfTimeInState(TestMachineState.Maintenance, asOfTime);

        // Proportion of Idle = (recorded Idle duration 10) / (total time 20)
        // Proportion = 10 / 20 = 0.5
        var propIdle = metric.GetProportionOfTimeInState(TestMachineState.Idle, asOfTime);

        // Proportion of Busy = (recorded Busy duration 5) / (total time 20)
        // Proportion = 5 / 20 = 0.25
        var propBusy = metric.GetProportionOfTimeInState(TestMachineState.Busy, asOfTime);


        // Assert
        AssertHelpers.AreEqual(0.25, propMaintenance);
        AssertHelpers.AreEqual(0.5, propIdle);
        AssertHelpers.AreEqual(0.25, propBusy);
    }

    [Fact(DisplayName = "GetProportionOfTime should include ongoing duration for the current state.")]
    public void GetProportionOfTime_ForCurrentState_IncludesOngoingDuration()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);
        metric.UpdateState(TestMachineState.Busy, 10.0); // CurrentTime=10, CurrentState=Busy
        var asOfTime = 25.0; // Total time is 25s.

        // Act
        // Proportion of Busy = (ongoing duration from 10 to 25 = 15s) / (total time 25s) = 0.6
        var propBusy = metric.GetProportionOfTimeInState(TestMachineState.Busy, asOfTime);

        // Assert
        AssertHelpers.AreEqual(0.6, propBusy);
    }

    [Fact(DisplayName ="GetProportionOfTime should calculate correctly for a state that has completed its duration.")]
    public void GetProportionOfTime_ForCompletedState_CalculatesCorrectly()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);
        metric.UpdateState(TestMachineState.Busy, 10.0); // Idle ran for 10s. CurrentTime = 10.
        var asOfTime = 20.0; // Total time is 20s.

        // Act
        // Proportion of Idle = (recorded duration 10s) / (total time 20s) = 0.5
        var propIdle = metric.GetProportionOfTimeInState(TestMachineState.Idle, asOfTime);

        // Assert
        AssertHelpers.AreEqual(0.5, propIdle);
    }

    [Fact(DisplayName = "GetProportionOfTime should be correct when 'asOfTime' equals the last event time.")]
    public void GetProportionOfTimeInState_AsOfTimeEqualsCurrentTime_CorrectProportion()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0); // InitialTime = 0
        metric.UpdateState(TestMachineState.Busy, 10.0);  // Idle for 10s. CurrentTime=10, CurrentState=Busy
        // StateDurations: { Idle: 10 }

        var asOfTime = 10.0; // Same as CurrentTime

        // Act
        // Proportion of Busy (current state): (ongoing Busy from 10 to 10 = 0s) / (total 10-0) = 0 / 10 = 0
        var propBusy = metric.GetProportionOfTimeInState(TestMachineState.Busy, asOfTime);
        // Proportion of Idle: (recorded Idle 10s) / (total 10s) = 1.0
        var propIdle = metric.GetProportionOfTimeInState(TestMachineState.Idle, asOfTime);

        // Assert
        AssertHelpers.AreEqual(0.0, propBusy);
        AssertHelpers.AreEqual(1.0, propIdle);
    }

    [Fact(DisplayName = "GetProportionOfTime should throw if 'asOfTime' is before the initial time.")]
    public void GetProportionOfTimeInState_AsOfTimeBeforeInitialTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 10.0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.GetProportionOfTimeInState(TestMachineState.Idle, 5.0));
    }

    [Fact(DisplayName = "GetProportionOfTime should throw if 'asOfTime' is before the warm-up time.")]
    public void GetProportionOfTime_AsOfTimeBeforeWarmupTime_ThrowsException()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);
        metric.UpdateState(TestMachineState.Busy, 50.0);

        metric.WarmedUp(100.0); // New InitialTime is 100.0

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            metric.GetProportionOfTimeInState(TestMachineState.Busy, 90.0) // Querying before InitialTime
        );
        Assert.Equal("asOfClockTime", ex.ParamName);
    }

    [Fact(DisplayName = "GetProportionOfTime should return 1.0 for the initial state when total time is zero.")]
    public void GetProportionOfTimeInState_TotalObservedTimeIsZero_AtInitialTime()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 5.0); // InitialTime = 5.0

        // Act
        // Querying at the exact initial time.
        var propIdle = metric.GetProportionOfTimeInState(TestMachineState.Idle, 5.0);
        var propBusy = metric.GetProportionOfTimeInState(TestMachineState.Busy, 5.0);

        // Assert
        AssertHelpers.AreEqual(1.0, propIdle); // Entity is in Idle state for 100% of the zero duration.
        AssertHelpers.AreEqual(0.0, propBusy);
    }

    [Fact(DisplayName = "GetProportionOfTime should calculate correctly for a past timestamp when history is enabled.")]
    public void GetProportionOfTime_ForPastTimestampWithHistory_CalculatesCorrectly()
    {
        // Arrange
        // CRITICAL: We must enable history for this feature to work correctly.
        var metric = new StateDurationMetric<TestMachineState>(
            TestMachineState.Idle,
            0.0,
            true
        );

        metric.UpdateState(TestMachineState.Busy, 10.0); // Idle duration: 10s (from t=0 to t=10)
        metric.UpdateState(TestMachineState.Idle, 20.0); // Busy duration: 10s (from t=10 to t=20)

        var asOfTime = 15.0; // We are querying for a time in the past.

        // Act
        // Human logic: At t=15, the machine had been Idle for 10s and Busy for 5s. Total time is 15s.

        // Proportion of Idle time at t=15 should be 10.0 / 15.0
        var propIdle = metric.GetProportionOfTimeInState(TestMachineState.Idle, asOfTime);

        // Proportion of Busy time at t=15 should be 5.0 / 15.0
        var propBusy = metric.GetProportionOfTimeInState(TestMachineState.Busy, asOfTime);

        // Assert
        AssertHelpers.AreEqual(10.0 / 15.0, propIdle);
        AssertHelpers.AreEqual(5.0 / 15.0, propBusy);
    }
}