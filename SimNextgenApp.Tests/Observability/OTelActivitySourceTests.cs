using System.Diagnostics;
using Moq;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Modeling;
using SimNextgenApp.Observability.Advanced;
using SimNextgenApp.Observability.Internal;
using SimNextgenApp.Observability.VolumeEstimation;

namespace SimNextgenApp.Tests.Observability;

public class OTelActivitySourceTests
{
    // What the OpenTelemetry SDK returns for a span its sampler drops: the Activity is still created
    // so context propagates, but IsAllDataRequested and Recorded are false.
    private const ActivitySamplingResult SampledOut = ActivitySamplingResult.PropagationData;

    private static SimulationProfile CreateProfile() => new(
        new Mock<ISimulationModel>().Object,
        new Mock<IRunStrategy>().Object,
        "TestProfile");

    [Theory(DisplayName = "CreateEventSpan should not tag a sampled-out span.")]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateEventSpan_SampledOut_IsNotTagged(bool enableTraceContext)
    {
        // Arrange
        using var isolated = new IsolatedActivitySource(SampledOut);
        var activitySource = new OTelActivitySource(enableTraceContext: enableTraceContext, source: isolated.Source);

        // Act
        using var scope = activitySource.CreateEventSpan("TestEvent", clockTime: 5, eventId: 42, isWarmupPhase: true);

        // Assert
        Assert.NotNull(scope.Span);
        Assert.False(scope.Span.IsAllDataRequested);
        Assert.Same(scope.Span, Activity.Current);
        Assert.Empty(scope.Span.TagObjects);
    }

    [Fact(DisplayName = "CreateEventSpan should not count a sampled-out span towards volume or cardinality.")]
    public void CreateEventSpan_SampledOut_IsNotCounted()
    {
        // Arrange
        using var isolated = new IsolatedActivitySource(SampledOut);
        using var volumeEstimator = new VolumeEstimator(VolumeThresholds.Default());
        using var cardinalityGuard = new CardinalityGuard();
        var activitySource = new OTelActivitySource(volumeEstimator, cardinalityGuard, source: isolated.Source);

        // Act
        using (activitySource.CreateEventSpan("TestEvent", clockTime: 5, eventId: 42, isWarmupPhase: false)) { }

        // Assert
        Assert.Equal(0, volumeEstimator.TotalSpans);
        Assert.Equal(0, cardinalityGuard.TotalUniqueValues);
    }

    [Fact(DisplayName = "CreateEventSpan should tag and count a recorded span.")]
    public void CreateEventSpan_Recorded_IsTaggedAndCounted()
    {
        // Arrange
        using var isolated = new IsolatedActivitySource();
        using var volumeEstimator = new VolumeEstimator(VolumeThresholds.Default());
        using var cardinalityGuard = new CardinalityGuard();
        var activitySource = new OTelActivitySource(volumeEstimator, cardinalityGuard, source: isolated.Source);

        // Act
        using var scope = activitySource.CreateEventSpan("TestEvent", clockTime: 5, eventId: 42, isWarmupPhase: false);

        // Assert
        Assert.NotNull(scope.Span);
        Assert.Equal("42", scope.Span.GetTagItem("sna.event.id"));
        Assert.Equal(5L, scope.Span.GetTagItem("sna.simulation.time"));
        Assert.Equal(false, scope.Span.GetTagItem("sna.simulation.warmup"));
        Assert.Equal(1, volumeEstimator.TotalSpans);
        Assert.Equal(1, cardinalityGuard.TotalUniqueValues);
    }

    [Fact(DisplayName = "CreateEventSpan should tag but not count a span that is recorded but not sampled.")]
    public void CreateEventSpan_RecordOnly_IsTaggedButNotCounted()
    {
        // Arrange
        using var isolated = new IsolatedActivitySource(ActivitySamplingResult.AllData);
        using var volumeEstimator = new VolumeEstimator(VolumeThresholds.Default());
        using var cardinalityGuard = new CardinalityGuard();
        var activitySource = new OTelActivitySource(volumeEstimator, cardinalityGuard, source: isolated.Source);

        // Act
        using var scope = activitySource.CreateEventSpan("TestEvent", clockTime: 5, eventId: 42, isWarmupPhase: false);

        // Assert
        Assert.NotNull(scope.Span);
        Assert.Equal("42", scope.Span.GetTagItem("sna.event.id"));
        Assert.Equal(0, volumeEstimator.TotalSpans);
        Assert.Equal(0, cardinalityGuard.TotalUniqueValues);
    }

    [Fact(DisplayName = "CreateEventSpan should neither tag nor count a sampled-out span that has the Recorded flag set.")]
    public void CreateEventSpan_SampledOutButFlaggedRecorded_IsNotTaggedOrCounted()
    {
        // Arrange
        // OpenTelemetry never passes such a span to its processors, so it is never exported
        using var isolated = new IsolatedActivitySource(
            SampledOut,
            onStarted: activity => activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded);
        using var volumeEstimator = new VolumeEstimator(VolumeThresholds.Default());
        using var cardinalityGuard = new CardinalityGuard();
        var activitySource = new OTelActivitySource(volumeEstimator, cardinalityGuard, source: isolated.Source);

        // Act
        using var scope = activitySource.CreateEventSpan("TestEvent", clockTime: 5, eventId: 42, isWarmupPhase: false);

        // Assert
        Assert.NotNull(scope.Span);
        Assert.False(scope.Span.IsAllDataRequested);
        Assert.True(scope.Span.Recorded);
        Assert.Empty(scope.Span.TagObjects);
        Assert.Equal(0, volumeEstimator.TotalSpans);
        Assert.Equal(0, cardinalityGuard.TotalUniqueValues);
    }

    [Fact(DisplayName = "CreateSimulationSpan should neither tag nor count a sampled-out span.")]
    public void CreateSimulationSpan_SampledOut_IsNotTaggedOrCounted()
    {
        // Arrange
        using var isolated = new IsolatedActivitySource(SampledOut);
        using var volumeEstimator = new VolumeEstimator(VolumeThresholds.Default());
        var activitySource = new OTelActivitySource(volumeEstimator, source: isolated.Source);

        // Act
        using var span = activitySource.CreateSimulationSpan(CreateProfile());

        // Assert
        Assert.NotNull(span);
        Assert.Empty(span.TagObjects);
        Assert.Equal(0, volumeEstimator.TotalSpans);
    }

    [Fact(DisplayName = "CreateWarmupSpan should neither tag nor count a sampled-out span.")]
    public void CreateWarmupSpan_SampledOut_IsNotTaggedOrCounted()
    {
        // Arrange
        using var isolated = new IsolatedActivitySource(SampledOut);
        using var volumeEstimator = new VolumeEstimator(VolumeThresholds.Default());
        var activitySource = new OTelActivitySource(volumeEstimator, source: isolated.Source);

        // Act
        using var span = activitySource.CreateWarmupSpan(warmupEndTime: 10);

        // Assert
        Assert.NotNull(span);
        Assert.Empty(span.TagObjects);
        Assert.Equal(0, volumeEstimator.TotalSpans);
    }
}
