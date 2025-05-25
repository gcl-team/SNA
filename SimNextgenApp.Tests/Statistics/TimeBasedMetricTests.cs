using SimNextgenApp.Statistics;

namespace SimNextgenApp.Tests.Statistics;

public class TimeBasedMetricTests
{
    [Fact]
    public void Constructor_DefaultInitialization_PropertiesAreSetCorrectly()
    {
        // Arrange & Act
        var metric = new TimeBasedMetric();

        // Assert
        AssertHelpers.AreEqual(0.0, metric.InitialTime);
        AssertHelpers.AreEqual(0.0, metric.CurrentTime);
        AssertHelpers.AreEqual(0.0, metric.CurrentCount);
        AssertHelpers.AreEqual(0.0, metric.TotalIncrementObserved);
        AssertHelpers.AreEqual(0.0, metric.TotalDecrementObserved);
        AssertHelpers.AreEqual(0.0, metric.TotalActiveDuration);
        AssertHelpers.AreEqual(0.0, metric.CumulativeCountTimeProduct);
        Assert.False(metric.IsHistoryEnabled);
        Assert.NotNull(metric.TimePerCount);
        Assert.Empty(metric.TimePerCount);
        Assert.NotNull(metric.History);
        Assert.Empty(metric.History);
    }

    [Fact]
    public void Constructor_WithInitialTimeAndHistory_PropertiesAreSetCorrectly()
    {
        // Arrange & Act
        var metric = new TimeBasedMetric(initialTime: 10.0, enableHistory: true);

        // Assert
        AssertHelpers.AreEqual(10.0, metric.InitialTime);
        AssertHelpers.AreEqual(10.0, metric.CurrentTime);
        Assert.True(metric.IsHistoryEnabled);
        Assert.NotNull(metric.History);
    }

    [Fact]
    public void ObserveCount_SingleObservation_UpdatesMetricsCorrectly()
    {
        // Arrange
        var metric = new TimeBasedMetric(initialTime: 0.0);

        // Act
        metric.ObserveCount(count: 5.0, clockTime: 10.0);

        // Assert
        AssertHelpers.AreEqual(10.0, metric.CurrentTime);
        AssertHelpers.AreEqual(5.0, metric.CurrentCount);
        AssertHelpers.AreEqual(5.0, metric.TotalIncrementObserved); // From 0 to 5
        AssertHelpers.AreEqual(0.0, metric.TotalDecrementObserved);
        AssertHelpers.AreEqual(10.0, metric.TotalActiveDuration); // Duration from 0 to 10
        AssertHelpers.AreEqual(0.0 * 10.0, metric.CumulativeCountTimeProduct); // Count was 0 for 10s
        AssertHelpers.AreEqual(0.0, metric.AverageCount); // (0*10)/10

        Assert.True(metric.TimePerCount.ContainsKey(0), "TimePerCount should contain key 0");
        AssertHelpers.AreEqual(10.0, metric.TimePerCount[0]);
    }

    [Fact]
    public void ObserveCount_MultipleObservations_IncrementAndDecrement_MetricsCorrect()
    {
        // Arrange
        var metric = new TimeBasedMetric(initialTime: 0.0);

        // Act
        metric.ObserveCount(count: 5.0, clockTime: 10.0); // Count 0 for 10s, then becomes 5
        metric.ObserveCount(count: 8.0, clockTime: 15.0); // Count 5 for 5s (15-10), then becomes 8
        metric.ObserveCount(count: 6.0, clockTime: 20.0); // Count 8 for 5s (20-15), then becomes 6

        // Assert
        AssertHelpers.AreEqual(20.0, metric.CurrentTime);
        AssertHelpers.AreEqual(6.0, metric.CurrentCount);

        // Increments: (5-0) + (8-5) = 5 + 3 = 8
        // Decrements: (8-6) = 2
        AssertHelpers.AreEqual(8.0, metric.TotalIncrementObserved);
        AssertHelpers.AreEqual(2.0, metric.TotalDecrementObserved);

        // Durations:
        // Time 0-10: Count 0. Duration = 10. Product = 0 * 10 = 0
        // Time 10-15: Count 5. Duration = 5. Product = 5 * 5 = 25
        // Time 15-20: Count 8. Duration = 5. Product = 8 * 5 = 40
        AssertHelpers.AreEqual(10.0 + 5.0 + 5.0, metric.TotalActiveDuration); // 20
        AssertHelpers.AreEqual(0 + 25 + 40, metric.CumulativeCountTimeProduct); // 65

        AssertHelpers.AreEqual(65.0 / 20.0, metric.AverageCount); // 3.25

        // TimePerCount
        // Count 0: 10s
        // Count 5: 5s
        // Count 8: 5s
        AssertHelpers.AreEqual(3, metric.TimePerCount.Count);
        AssertHelpers.AreEqual(10.0, metric.TimePerCount[0]);
        AssertHelpers.AreEqual(5.0, metric.TimePerCount[5]);
        AssertHelpers.AreEqual(5.0, metric.TimePerCount[8]);
    }

    [Fact]
    public void ObserveCount_ObservationAtSameTime_NoDurationChange_CountUpdates()
    {
        // Arrange
        var metric = new TimeBasedMetric(initialTime: 0.0);
        metric.ObserveCount(count: 5.0, clockTime: 10.0); // Initial state: CurrentTime=10, CurrentCount=5, TotalActiveDuration=10, CCTP=0

        // Act
        metric.ObserveCount(count: 7.0, clockTime: 10.0); // Observe again at the same time

        // Assert
        AssertHelpers.AreEqual(10.0, metric.CurrentTime);
        AssertHelpers.AreEqual(7.0, metric.CurrentCount); // Count updated
        AssertHelpers.AreEqual(5.0 + (7.0 - 5.0), metric.TotalIncrementObserved); // Increment 5 (0->5) + 2 (5->7) = 7
        AssertHelpers.AreEqual(10.0, metric.TotalActiveDuration); // Duration should not change
        AssertHelpers.AreEqual(0.0, metric.CumulativeCountTimeProduct);  // CCTP should not change
        AssertHelpers.AreEqual(0.0, metric.AverageCount); // (0*10)/10

        // TimePerCount should still reflect duration for count 0, as no new duration passed
        AssertHelpers.AreEqual(1, metric.TimePerCount.Count);
        AssertHelpers.AreEqual(10.0, metric.TimePerCount[0]);
    }


    [Fact]
    public void ObserveChange_PositiveChange_MetricsCorrect()
    {
        // Arrange
        var metric = new TimeBasedMetric(initialTime: 0.0);
        metric.ObserveCount(count: 5.0, clockTime: 10.0); // Count 0 for 10s, CCTP=0. Then count becomes 5. TotalIncrement=5.

        // Act
        metric.ObserveChange(change: 3.0, clockTime: 15.0); // Count 5 for 5s, CCTP += 5*5=25. Then count becomes 8. TotalIncrement += 3.

        // Assert
        AssertHelpers.AreEqual(15.0, metric.CurrentTime);
        AssertHelpers.AreEqual(8.0, metric.CurrentCount); // 5 + 3
        AssertHelpers.AreEqual(5.0 + 3.0, metric.TotalIncrementObserved); // Initial 5 + change 3
        AssertHelpers.AreEqual(10.0 + 5.0, metric.TotalActiveDuration); // 15
        AssertHelpers.AreEqual(0.0 + (5.0 * 5.0), metric.CumulativeCountTimeProduct); // 25
        AssertHelpers.AreEqual(25.0 / 15.0, metric.AverageCount);
    }

    [Fact]
    public void ObserveChange_NegativeChange_MetricsCorrect()
    {
        // Arrange
        var metric = new TimeBasedMetric(initialTime: 0.0);
        metric.ObserveCount(count: 10.0, clockTime: 5.0); // Count 0 for 5s. TotalIncrement=10.

        // Act
        metric.ObserveChange(change: -4.0, clockTime: 12.0); // Count 10 for 7s. CCTP += 10*7=70. Then count becomes 6. TotalDecrement=4.

        // Assert
        AssertHelpers.AreEqual(12.0, metric.CurrentTime);
        AssertHelpers.AreEqual(6.0, metric.CurrentCount); // 10 - 4
        AssertHelpers.AreEqual(10.0, metric.TotalIncrementObserved);
        AssertHelpers.AreEqual(4.0, metric.TotalDecrementObserved);
        AssertHelpers.AreEqual(5.0 + 7.0, metric.TotalActiveDuration); // 12
        AssertHelpers.AreEqual(0.0 + (10.0 * 7.0), metric.CumulativeCountTimeProduct); // 70
        AssertHelpers.AreEqual(70.0 / 12.0, metric.AverageCount);
    }

    [Fact]
    public void Rates_Calculation_CorrectAfterObservations()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        metric.ObserveCount(5, 10); // Inc: 5. Dur: 10
        metric.ObserveCount(2, 20); // Dec: 3. Dur: 10 (at count 5). TotalDur: 20.

        // Assert
        // TotalIncrement = 5. TotalDecrement = 3. TotalActiveDuration = 10 (at count 0) + 10 (at count 5) = 20
        AssertHelpers.AreEqual(5.0 / 20.0, metric.IncrementRate);
        AssertHelpers.AreEqual(3.0 / 20.0, metric.DecrementRate);
    }

    [Fact]
    public void Rates_NoDuration_RatesAreZero()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        metric.ObserveCount(5, 0); // Observation at initial time, no duration passed yet.

        // Assert
        AssertHelpers.AreEqual(0.0, metric.IncrementRate); // TotalActiveDuration is 0
        AssertHelpers.AreEqual(0.0, metric.DecrementRate); // TotalActiveDuration is 0
    }

    [Fact]
    public void AverageSojournTime_Calculation_Correct()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        // L = AverageCount, Lambda = DecrementRate, W = AverageSojournTime (W = L/Lambda)
        metric.ObserveCount(10, 10);  // Count 0 for 10s. CCTP = 0. TotalDur = 10. Inc = 10.
                                     // CurrentCount = 10
        metric.ObserveCount(5, 20);   // Count 10 for 10s. CCTP += 100. TotalDur = 20. Dec = 5.
                                     // CurrentCount = 5
        metric.ObserveCount(7, 30);   // Count 5 for 10s. CCTP += 50. TotalDur = 30. Inc = 2.
                                     // CurrentCount = 7

        // Final State:
        // TotalIncrementObserved = 10 + 2 = 12
        // TotalDecrementObserved = 5
        // TotalActiveDuration = 10 (at 0) + 10 (at 10) + 10 (at 5) = 30
        // CumulativeCountTimeProduct = (0*10) + (10*10) + (5*10) = 0 + 100 + 50 = 150
        // AverageCount = 150 / 30 = 5
        // DecrementRate = TotalDecrementObserved / TotalActiveDuration = 5 / 30

        double expectedAvgCount = 150.0 / 30.0;
        double expectedDecRate = 5.0 / 30.0;
        AssertHelpers.AreEqual(expectedAvgCount / expectedDecRate, metric.AverageSojournTime);
    }

    [Fact]
    public void AverageSojournTime_ZeroDecrementRate_ReturnsZero()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        metric.ObserveCount(5, 10); // Only increments

        // Assert
        AssertHelpers.AreEqual(0.0, metric.AverageSojournTime);
    }


    [Fact]
    public void WarmedUp_ResetsMetrics_PreservesHistorySetting()
    {
        // Arrange
        var metric = new TimeBasedMetric(initialTime: 0.0, enableHistory: true);
        metric.ObserveCount(5.0, 10.0);
        metric.ObserveCount(3.0, 15.0);

        // Act
        metric.WarmedUp(clockTime: 20.0);

        // Assert
        AssertHelpers.AreEqual(20.0, metric.InitialTime);
        AssertHelpers.AreEqual(20.0, metric.CurrentTime);
        AssertHelpers.AreEqual(0.0, metric.CurrentCount); // Resets to 0
        AssertHelpers.AreEqual(0.0, metric.TotalIncrementObserved);
        AssertHelpers.AreEqual(0.0, metric.TotalDecrementObserved);
        AssertHelpers.AreEqual(0.0, metric.TotalActiveDuration);
        AssertHelpers.AreEqual(0.0, metric.CumulativeCountTimeProduct);
        Assert.True(metric.IsHistoryEnabled, "IsHistoryEnabled should be preserved");
        Assert.NotNull(metric.TimePerCount);
        Assert.Empty(metric.TimePerCount);
        Assert.NotNull(metric.History);
        Assert.Empty(metric.History);
    }

    [Fact]
    public void History_Enabled_RecordsObservations()
    {
        // Arrange
        var metric = new TimeBasedMetric(enableHistory: true);

        // Act
        metric.ObserveCount(5, 10);
        metric.ObserveChange(-2, 15); // Count becomes 3

        // Assert
        Assert.Equal(2, metric.History.Count);
        AssertHelpers.AreEqual(10.0, metric.History[0].Time);
        AssertHelpers.AreEqual(5.0, metric.History[0].CountValue);
        AssertHelpers.AreEqual(15.0, metric.History[1].Time);
        AssertHelpers.AreEqual(3.0, metric.History[1].CountValue);
    }

    [Fact]
    public void History_Disabled_DoesNotRecordObservations()
    {
        // Arrange
        var metric = new TimeBasedMetric(enableHistory: false);

        // Act
        metric.ObserveCount(5, 10);
        metric.ObserveChange(-2, 15);

        // Assert
        Assert.Empty(metric.History);
    }

    [Fact]
    public void GetCountPercentileByTime_VariousScenarios()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        // Count 0 for 10s (0-10)
        // Count 2 for 30s (10-40)
        // Count 5 for 60s (40-100)
        metric.ObserveCount(2, 10); // At t=10, count becomes 2. Before that, count was 0 for 10s.
        metric.ObserveCount(5, 40); // At t=40, count becomes 5. Before that, count was 2 for 30s.
        metric.ObserveCount(5, 100); // At t=100, count remains 5. Before that, count was 5 for 60s.

        // TimePerCount: {0: 10, 2: 30, 5: 60}
        // TotalActiveDuration = 10 + 30 + 60 = 100

        // Assert
        Assert.Equal(0, metric.GetCountPercentileByTime(0));        // Smallest count
        Assert.Equal(0, metric.GetCountPercentileByTime(5));        // 100 * 0.05 = 5. Falls in count 0
        Assert.Equal(0, metric.GetCountPercentileByTime(10));       // 100 * 0.10 = 10. Falls in count 0
        Assert.Equal(2, metric.GetCountPercentileByTime(11));       // 100 * 0.11 = 11. Falls in count 2 (10 for 0 + 1 for 2)
        Assert.Equal(2, metric.GetCountPercentileByTime(40));       // 100 * 0.40 = 40. Falls in count 2 (10 for 0 + 30 for 2)
        Assert.Equal(5, metric.GetCountPercentileByTime(41));       // 100 * 0.41 = 41. Falls in count 5
        Assert.Equal(5, metric.GetCountPercentileByTime(100));      // Max count
    }

    [Fact]
    public void GetCountPercentileByTime_EmptyData_ReturnsDefaultOrThrows()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        // Assert - current behavior is to return 0 for empty data
        Assert.Equal(0, metric.GetCountPercentileByTime(50));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    public void GetCountPercentileByTime_InvalidRatio_ThrowsArgumentOutOfRangeException(double invalidRatio)
    {
        // Arrange
        var metric = new TimeBasedMetric();
        metric.ObserveCount(1,1); // Add some data

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.GetCountPercentileByTime(invalidRatio));
    }


    [Fact]
    public void GenerateHistogram_BasicCase_CorrectBins()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        metric.ObserveCount(1, 10); // 0 for 10s
        metric.ObserveCount(3, 20); // 1 for 10s
        metric.ObserveCount(6, 30); // 3 for 10s
        // TimePerCount: {0:10, 1:10, 3:10}. TotalActiveDuration = 30
        // (Count 6 is current, but duration for it hasn't passed yet for TimePerCount)

        // Act
        var histogram = metric.GenerateHistogram(countIntervalWidth: 2.0);
        // Expected Bins:
        // [0, 2): Count 0 (10s), Count 1 (10s). Total = 20s. Prob = 20/30. CumProb = 20/30
        // [2, 4): Count 3 (10s). Total = 10s. Prob = 10/30. CumProb = 30/30
        // [4, 6): (empty)
        // [6, 8): (empty, current count 6 not yet accounted for in TimePerCount with duration)

        // Assert
        Assert.Equal(2, histogram.Count);
        // Bins cover up to max KEY in TimePerCount (3)
        // Actual max observed count is 3 in TimeForCount. So bins [0,2), [2,4)

        // Bin 1: [0, 2)
        AssertHelpers.AreEqual(0.0, histogram[0].CountLowerBound);
        AssertHelpers.AreEqual(10.0 + 10.0, histogram[0].TotalTime); // Time for count 0 and 1
        AssertHelpers.AreEqual((10.0 + 10.0) / 30.0, histogram[0].Probability);
        AssertHelpers.AreEqual((10.0 + 10.0) / 30.0, histogram[0].CumulativeProbability);

        // Bin 2: [2, 4)
        AssertHelpers.AreEqual(2.0, histogram[1].CountLowerBound);
        AssertHelpers.AreEqual(10.0, histogram[1].TotalTime); // Time for count 3
        AssertHelpers.AreEqual(10.0 / 30.0, histogram[1].Probability);
        AssertHelpers.AreEqual((20.0 + 10.0) / 30.0, histogram[1].CumulativeProbability);
    }

    [Fact]
    public void GenerateHistogram_NonZeroMinCount_CorrectBins()
    {
        var metric = new TimeBasedMetric(initialTime: 0);
        metric.ObserveCount(5, 10);  // Count 0 for 10s. CurrentCount = 5
        metric.ObserveCount(7, 20);  // Count 5 for 10s. CurrentCount = 7
        metric.ObserveCount(7, 30);  // Count 7 for 10s. CurrentCount = 7
        // TimePerCount: {0:10, 5:10, 7:10}. TotalActiveDuration = 30.

        var histogram = metric.GenerateHistogram(countIntervalWidth: 2.0);
        // MinObservedCount (key in TimePerCount) = 0. MaxObservedCount = 7
        // Bins starting from floor(0/2)*2 = 0
        // [0, 2): count 0 (10s). P=10/30. CP=10/30
        // [2, 4): (empty). P=0. CP=10/30
        // [4, 6): count 5 (10s). P=10/30. CP=20/30
        // [6, 8): count 7 (10s). P=10/30. CP=30/30

        Assert.Equal(4, histogram.Count);
        AssertHelpers.AreEqual(0.0, histogram[0].CountLowerBound); AssertHelpers.AreEqual(10.0, histogram[0].TotalTime);
        AssertHelpers.AreEqual(2.0, histogram[1].CountLowerBound); AssertHelpers.AreEqual(0.0, histogram[1].TotalTime);
        AssertHelpers.AreEqual(4.0, histogram[2].CountLowerBound); AssertHelpers.AreEqual(10.0, histogram[2].TotalTime);
        AssertHelpers.AreEqual(6.0, histogram[3].CountLowerBound); AssertHelpers.AreEqual(10.0, histogram[3].TotalTime);

        AssertHelpers.AreEqual(1.0, histogram.Last().CumulativeProbability);
    }


    [Fact]
    public void GenerateHistogram_EmptyData_ReturnsEmptyList()
    {
        // Arrange
        var metric = new TimeBasedMetric();

        // Act
        var histogram = metric.GenerateHistogram(countIntervalWidth: 1.0);

        // Assert
        Assert.NotNull(histogram);
        Assert.Empty(histogram);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void GenerateHistogram_InvalidInterval_ThrowsArgumentOutOfRangeException(double invalidInterval)
    {
        // Arrange
        var metric = new TimeBasedMetric();
        metric.ObserveCount(1,1); // Add some data

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.GenerateHistogram(invalidInterval));
    }

    [Fact]
    public void ObserveCount_TimeGoesBackwards_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var metric = new TimeBasedMetric();
        metric.ObserveCount(5, 10);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => metric.ObserveCount(6, 5));
    }


    [Fact]
    public void ObservationCoverageRatio_FullCoverage()
    {
        var metric = new TimeBasedMetric(0);
        metric.ObserveCount(1, 10); // ActiveDuration = 10. CurrentTime = 10. InitialTime = 0.
        // Ratio = 10 / (10-0) = 1
        AssertHelpers.AreEqual(1.0, metric.ObservationCoverageRatio);
    }

    [Fact]
    public void ObservationCoverageRatio_PartialCoverageDueToInitialTime()
    {
        var metric = new TimeBasedMetric(5); // InitialTime = 5
        metric.ObserveCount(1, 10); // ActiveDuration = 5 (from 5 to 10). CurrentTime = 10. InitialTime = 5.
        // Ratio = 5 / (10-5) = 1
        AssertHelpers.AreEqual(1.0, metric.ObservationCoverageRatio);
    }

    [Fact]
    public void ObservationCoverageRatio_NoObservationsAfterInit_IsZero()
    {
        var metric = new TimeBasedMetric(initialTime: 0, enableHistory: false);
        // No ObserveCount calls yet, but let's imagine CurrentTime somehow advanced (not directly possible with public API)
        // For this test, we focus on the calculation.
        // If CurrentTime == InitialTime, it's 0.
        // Let's test if CurrentTime > InitialTime but TotalActiveDuration is 0
        // This state is hard to achieve naturally without internal manipulation or a flawed ObserveCount.
        // The existing logic will always have TotalActiveDuration match CurrentTime-InitialTime if observations span the period.
        // So, if CurrentTime != InitialTime, TotalActiveDuration would be CurrentTime-InitialTime.
        // The only way for it to be less is if ObserveCount wasn't called to cover some span.
        // However, with the current API, ObserveCount implicitly covers the span.
        // Let's test the edge case where TotalActiveDuration is somehow zero (e.g., only one observation at InitialTime)
        // and CurrentTime later moves (hypothetically).

        // Directly, after construction:
        AssertHelpers.AreEqual(0.0, metric.ObservationCoverageRatio);

        metric.ObserveCount(1,0); // Observe at initial time
        AssertHelpers.AreEqual(0.0, metric.ObservationCoverageRatio);
    }
}
