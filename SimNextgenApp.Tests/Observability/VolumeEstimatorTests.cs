using Microsoft.Extensions.Time.Testing;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Tests.Observability;

public class VolumeEstimatorTests
{
    [Fact(DisplayName = "Constructor should initialize estimator with configured thresholds.")]
    public void Constructor_InitializesWithThresholds()
    {
        // Arrange
        var thresholds = new VolumeThresholds(spansPerSecond: 1000, metricDataPointsPerSecond: 5000);

        // Act
        using var estimator = new VolumeEstimator(thresholds);

        // Assert
        Assert.NotNull(estimator);
        Assert.Equal(0, estimator.TotalSpans);
        Assert.Equal(0, estimator.TotalMetricDataPoints);
    }

    [Fact(DisplayName = "RecordSpan should increment total span count.")]
    public void RecordSpan_IncrementsSpanCount()
    {
        // Arrange
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds);

        // Act
        estimator.RecordSpan();
        estimator.RecordSpan();
        estimator.RecordSpan();

        // Assert
        Assert.Equal(3, estimator.TotalSpans);
    }

    [Fact(DisplayName = "RecordSpans should increment span count by specified amount.")]
    public void RecordSpans_IncrementsByCount()
    {
        // Arrange
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds);

        // Act
        estimator.RecordSpans(10);
        estimator.RecordSpans(5);

        // Assert
        Assert.Equal(15, estimator.TotalSpans);
    }

    [Fact(DisplayName = "RecordMetricDataPoint should increment metric data point count.")]
    public void RecordMetricDataPoint_IncrementsMetricCount()
    {
        // Arrange
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds);

        // Act
        estimator.RecordMetricDataPoint();
        estimator.RecordMetricDataPoint();

        // Assert
        Assert.Equal(2, estimator.TotalMetricDataPoints);
    }

    [Fact(DisplayName = "RecordMetricDataPoints should increment metric count by specified amount.")]
    public void RecordMetricDataPoints_IncrementsByCount()
    {
        // Arrange
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds);

        // Act
        estimator.RecordMetricDataPoints(100);
        estimator.RecordMetricDataPoints(50);

        // Assert
        Assert.Equal(150, estimator.TotalMetricDataPoints);
    }

    [Fact(DisplayName = "SpansPerSecond should calculate rate correctly based on elapsed time.")]
    public void SpansPerSecond_CalculatesRateCorrectly()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds, fakeTime);

        // Act
        estimator.RecordSpans(1000);
        fakeTime.Advance(TimeSpan.FromSeconds(1));

        // Assert - Should be 1000 spans/sec
        Assert.Equal(1000, estimator.SpansPerSecond);
    }

    [Fact(DisplayName = "MetricDataPointsPerSecond should calculate rate correctly based on elapsed time.")]
    public void MetricDataPointsPerSecond_CalculatesRateCorrectly()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds, fakeTime);

        // Act
        estimator.RecordMetricDataPoints(5000);
        fakeTime.Advance(TimeSpan.FromSeconds(1));

        // Assert - Should be 5000 metrics/sec
        Assert.Equal(5000, estimator.MetricDataPointsPerSecond);
    }

    [Fact(DisplayName = "VolumeWarning should trigger when span rate exceeds threshold.")]
    public void VolumeWarning_TriggersWhenSpanThresholdExceeded()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var thresholds = new VolumeThresholds(spansPerSecond: 100, metricDataPointsPerSecond: 1000);
        using var estimator = new VolumeEstimator(thresholds, fakeTime);

        bool warningTriggered = false;
        VolumeWarningEventArgs? eventArgs = null;

        estimator.VolumeWarning += (sender, args) =>
        {
            warningTriggered = true;
            eventArgs = args;
        };

        // Act - Advance time then record spans to create rate > 100 spans/sec
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        estimator.RecordSpans(200); // 200 spans in 1 second = 200 spans/sec > 100 threshold

        // Assert
        Assert.True(warningTriggered);
        Assert.NotNull(eventArgs);
        Assert.Equal(VolumeWarningType.SpanRate, eventArgs.WarningType);
        Assert.True(eventArgs.CurrentRate > thresholds.SpansPerSecondThreshold);
    }

    [Fact(DisplayName = "VolumeWarning should trigger when metric rate exceeds threshold.")]
    public void VolumeWarning_TriggersWhenMetricThresholdExceeded()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var thresholds = new VolumeThresholds(spansPerSecond: 10000, metricDataPointsPerSecond: 100);
        using var estimator = new VolumeEstimator(thresholds, fakeTime);

        bool warningTriggered = false;
        VolumeWarningEventArgs? eventArgs = null;

        estimator.VolumeWarning += (sender, args) =>
        {
            warningTriggered = true;
            eventArgs = args;
        };

        // Act - Advance time then record metrics to create rate > 100 metrics/sec
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        estimator.RecordMetricDataPoints(200); // 200 metrics in 1 second = 200 metrics/sec > 100 threshold

        // Assert
        Assert.True(warningTriggered);
        Assert.NotNull(eventArgs);
        Assert.Equal(VolumeWarningType.MetricRate, eventArgs.WarningType);
        Assert.True(eventArgs.CurrentRate > thresholds.MetricDataPointsPerSecondThreshold);
    }

    [Fact(DisplayName = "VolumeWarning should only trigger once per threshold type.")]
    public void VolumeWarning_OnlyTriggersOnce()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var thresholds = new VolumeThresholds(spansPerSecond: 100, metricDataPointsPerSecond: 1000);
        using var estimator = new VolumeEstimator(thresholds, fakeTime);

        int warningCount = 0;
        estimator.VolumeWarning += (sender, args) => warningCount++;

        // Act - Advance time and record spans multiple times to exceed threshold
        fakeTime.Advance(TimeSpan.FromSeconds(1));
        estimator.RecordSpans(200); // First record - triggers warning
        estimator.RecordSpans(200); // Second record - should not trigger again
        estimator.RecordSpans(200); // Third record - should not trigger again

        // Assert - Warning should only trigger once
        Assert.Equal(1, warningCount);
    }

    [Fact(DisplayName = "Reset should clear all counters and warning states.")]
    public void Reset_ClearsAllCounters()
    {
        // Arrange
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds);

        estimator.RecordSpans(100);
        estimator.RecordMetricDataPoints(500);

        // Act
        estimator.Reset();

        // Assert
        Assert.Equal(0, estimator.TotalSpans);
        Assert.Equal(0, estimator.TotalMetricDataPoints);
        Assert.Equal(0, estimator.SpansPerSecond);
        Assert.Equal(0, estimator.MetricDataPointsPerSecond);
    }

    [Fact(DisplayName = "GetStatistics should return current volume data.")]
    public void GetStatistics_ReturnsCorrectData()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds, fakeTime);

        estimator.RecordSpans(100);
        estimator.RecordMetricDataPoints(500);
        fakeTime.Advance(TimeSpan.FromSeconds(2));

        // Act
        var stats = estimator.GetStatistics();

        // Assert
        Assert.Equal(100, stats.TotalSpans);
        Assert.Equal(500, stats.TotalMetricDataPoints);
        Assert.Equal(50, stats.SpansPerSecond); // 100 spans / 2 seconds = 50
        Assert.Equal(250, stats.MetricDataPointsPerSecond); // 500 metrics / 2 seconds = 250
        Assert.Equal(TimeSpan.FromSeconds(2), stats.ElapsedTime);
    }

    [Fact(DisplayName = "VolumeThresholds.Default should return standard threshold values.")]
    public void VolumeThresholds_Default_HasReasonableValues()
    {
        // Act
        var thresholds = VolumeThresholds.Default();

        // Assert
        Assert.Equal(10_000, thresholds.SpansPerSecondThreshold);
        Assert.Equal(50_000, thresholds.MetricDataPointsPerSecondThreshold);
    }

    [Fact(DisplayName = "VolumeThresholds.Conservative should return lower threshold values.")]
    public void VolumeThresholds_Conservative_HasLowerValues()
    {
        // Act
        var thresholds = VolumeThresholds.Conservative();

        // Assert
        Assert.Equal(1_000, thresholds.SpansPerSecondThreshold);
        Assert.Equal(5_000, thresholds.MetricDataPointsPerSecondThreshold);
    }

    [Fact(DisplayName = "VolumeThresholds.Permissive should return higher threshold values.")]
    public void VolumeThresholds_Permissive_HasHigherValues()
    {
        // Act
        var thresholds = VolumeThresholds.Permissive();

        // Assert
        Assert.Equal(100_000, thresholds.SpansPerSecondThreshold);
        Assert.Equal(500_000, thresholds.MetricDataPointsPerSecondThreshold);
    }

    [Theory(DisplayName = "VolumeThresholds should throw for invalid span threshold.")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void VolumeThresholds_ThrowsForInvalidSpanThreshold(double threshold)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VolumeThresholds(spansPerSecond: threshold, metricDataPointsPerSecond: 1000));
    }

    [Theory(DisplayName = "VolumeThresholds should throw for invalid metric threshold.")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void VolumeThresholds_ThrowsForInvalidMetricThreshold(double threshold)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VolumeThresholds(spansPerSecond: 1000, metricDataPointsPerSecond: threshold));
    }

    [Fact(DisplayName = "ElapsedTime should increase as time passes.")]
    public void ElapsedTime_IncreasesOverTime()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();
        var thresholds = VolumeThresholds.Default();
        using var estimator = new VolumeEstimator(thresholds, fakeTime);

        var initialTime = estimator.ElapsedTime;
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));

        // Act
        var laterTime = estimator.ElapsedTime;

        // Assert
        Assert.True(laterTime > initialTime);
        Assert.Equal(TimeSpan.FromMilliseconds(500), laterTime);
    }

    [Fact(DisplayName = "Dispose should clean up resources without throwing.")]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var thresholds = VolumeThresholds.Default();
        var estimator = new VolumeEstimator(thresholds);

        // Act & Assert
        estimator.Dispose();
    }
}
