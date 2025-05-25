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
    [Fact]
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

    [Fact]
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

    [Fact]
    public void Constructor_NegativeInitialTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var initialState = TestMachineState.Idle;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new StateDurationMetric<TestMachineState>(initialState, -1.0));
    }

    [Fact]
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

    [Fact]
    public void UpdateState_MultipleTransitions_AccumulatesDurationsCorrectly()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);

        // Act
        metric.UpdateState(TestMachineState.Busy, 10.0);       // Idle for 10s
        metric.UpdateState(TestMachineState.Maintenance, 15.0); // Busy for 5s
        metric.UpdateState(TestMachineState.Idle, 25.0);      // Maintenance for 10s

        // Assert
        AssertHelpers.AreEqual(25.0, metric.CurrentTime);
        Assert.Equal(TestMachineState.Idle, metric.CurrentState);

        AssertHelpers.AreEqual(10.0, metric.StateDurations[TestMachineState.Idle]);
        AssertHelpers.AreEqual(5.0, metric.StateDurations[TestMachineState.Busy]);
        AssertHelpers.AreEqual(10.0, metric.StateDurations[TestMachineState.Maintenance]);
    }

    [Fact]
    public void UpdateState_TransitionToSameStateAtLaterTime_AddsToDuration()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);

        // Act
        // This scenario is a bit unusual for state transitions, but the logic should handle it.
        // It implies the entity *remained* Idle, and we're just marking a point in time.
        // However, UpdateState is for *transitions*. A more typical way to advance time without state change
        // would be for GetProportionOfTimeInState to use a later asOfClockTime.
        // The current UpdateState implies a "refresh" or re-affirmation of the state.
        // The duration *before* this call in CurrentState (Idle) should be recorded.
        metric.UpdateState(TestMachineState.Idle, 5.0); // Idle for 5s

        // Assert
        AssertHelpers.AreEqual(5.0, metric.CurrentTime);
        Assert.Equal(TestMachineState.Idle, metric.CurrentState);
        Assert.True(metric.StateDurations.ContainsKey(TestMachineState.Idle));
        AssertHelpers.AreEqual(5.0, metric.StateDurations[TestMachineState.Idle]);
    }


    [Fact]
    public void UpdateState_TransitionAtSameTime_UpdatesStateNoDurationChange()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 5.0);

        // Act
        metric.UpdateState(TestMachineState.Busy, 5.0); // Transition at the exact same time

        // Assert
        AssertHelpers.AreEqual(5.0, metric.CurrentTime);
        Assert.Equal(TestMachineState.Busy, metric.CurrentState);
        Assert.False(metric.StateDurations.ContainsKey(TestMachineState.Idle)); // No duration for Idle as time didn't advance
    }

    [Fact]
    public void UpdateState_ClockTimeGoesBackwards_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 10.0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.UpdateState(TestMachineState.Busy, 5.0));
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public void WarmedUp_NegativeClockTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.WarmedUp(-5.0));
    }

    [Fact]
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

    [Fact]
    public void GetTotalDurationInState_StateEnteredMultipleTimes_ReturnsSum()
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

    [Fact]
    public void GetProportionOfTimeInState_BasicScenario_CorrectProportion()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0); // InitialTime = 0
        metric.UpdateState(TestMachineState.Busy, 10.0);  // Idle for 10s
        metric.UpdateState(TestMachineState.Maintenance, 15.0); // Busy for 5s
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

    [Fact]
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
    
    [Fact]
    public void GetProportionOfTimeInState_AsOfTimeBeforeInitialTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 10.0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.GetProportionOfTimeInState(TestMachineState.Idle, 5.0));
    }

    [Fact]
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
    
        [Fact]
    public void GetProportionOfTimeInState_QueryForPastTimeWhileCurrentlyInQueriedState() // Renaming for clarity
    {
        // Arrange
        var metric = new StateDurationMetric<TestMachineState>(TestMachineState.Idle, 0.0);
        metric.UpdateState(TestMachineState.Busy, 10.0); // Idle: 0-10 (10s). CurrentTime=10, CurrentState=Busy
                                                         // StateDurations[Idle]=10
        metric.UpdateState(TestMachineState.Idle, 20.0); // Busy: 10-20 (10s). CurrentTime=20, CurrentState=Idle
                                                         // StateDurations[Idle]=10 (no change), StateDurations[Busy]=10

        // State of metric after all updates:
        // InitialTime = 0.0
        // CurrentTime = 20.0
        // CurrentState = TestMachineState.Idle
        // StateDurations = { Idle: 10.0, Busy: 10.0 }

        // Test 1: Proportion of Idle at asOfClockTime = 15.0
        // At t=15, the entity was in state Busy.
        // Recorded duration for Idle is 10.0 (from 0-10).
        // totalObservedTimeSpan = 15.0 - 0.0 = 15.0
        // Proportion = 10.0 / 15.0
        var proportionIdleAt15 = metric.GetProportionOfTimeInState(TestMachineState.Idle, 15.0);
        AssertHelpers.AreEqual(10.0 / 15.0, proportionIdleAt15);

        // Test 2: Proportion of Busy at asOfClockTime = 15.0
        // At t=15, the entity was in state Busy.
        // CurrentState (final state of metric) is Idle, not Busy, so the special "ongoing" logic for final CurrentState is NOT triggered for Busy.
        // Recorded duration for Busy is 10.0 (from 10-20).
        // totalObservedTimeSpan = 15.0 - 0.0 = 15.0
        // Proportion = 10.0 / 15.0
        var proportionBusyAt15 = metric.GetProportionOfTimeInState(TestMachineState.Busy, 15.0);
        AssertHelpers.AreEqual(10.0 / 15.0, proportionBusyAt15); // << THE FIX IS HERE in the expected value

        // Test 3: Proportion of Idle at asOfClockTime = 20.0 (final time)
        // CurrentState (final state of metric) IS Idle. asOfClockTime (20) >= CurrentTime (20).
        // Recorded duration for Idle is 10.0.
        // Ongoing duration for Idle = asOfClockTime (20) - CurrentTime (20) = 0.0.
        // Total duration in Idle = 10.0 + 0.0 = 10.0.
        // totalObservedTimeSpan = 20.0 - 0.0 = 20.0
        // Proportion = 10.0 / 20.0 = 0.5
        var proportionIdleAt20 = metric.GetProportionOfTimeInState(TestMachineState.Idle, 20.0);
        AssertHelpers.AreEqual(10.0 / 20.0, proportionIdleAt20);

        // Test 4: Proportion of Busy at asOfClockTime = 20.0 (final time)
        // CurrentState (final state of metric) is Idle, not Busy.
        // Recorded duration for Busy is 10.0.
        // totalObservedTimeSpan = 20.0 - 0.0 = 20.0
        // Proportion = 10.0 / 20.0 = 0.5
        var proportionBusyAt20 = metric.GetProportionOfTimeInState(TestMachineState.Busy, 20.0);
        AssertHelpers.AreEqual(10.0 / 20.0, proportionBusyAt20);
    }
}