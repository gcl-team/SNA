using System.Diagnostics;

namespace SimNextgenApp.Tests.Observability;

/// <summary>
/// An <see cref="ActivitySource"/> with a unique name and a listener attached to that source only.
/// </summary>
/// <remarks>
/// ActivityListeners are process-wide and xUnit runs test classes in parallel. Tests that share a
/// source name, or listen to every source, can change each other's sampling decisions. Tests that
/// need activities should use this instead of creating an ActivitySource and ActivityListener directly.
/// </remarks>
public sealed class IsolatedActivitySource : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _startedActivities = [];

    public ActivitySource Source { get; }

    /// <summary>
    /// Every activity started from <see cref="Source"/>, in start order.
    /// </summary>
    public IReadOnlyList<Activity> StartedActivities => _startedActivities;

    /// <param name="samplingResult">The sampling decision the listener returns for every activity.</param>
    /// <param name="onStarted">Runs when each activity starts, before <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> returns it.</param>
    public IsolatedActivitySource(
        ActivitySamplingResult samplingResult = ActivitySamplingResult.AllDataAndRecorded,
        Action<Activity>? onStarted = null)
    {
        Source = new ActivitySource($"{nameof(IsolatedActivitySource)}.{Guid.NewGuid():N}");
        _listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, Source),
            Sample = (ref _) => samplingResult,
            ActivityStarted = activity =>
            {
                _startedActivities.Add(activity);
                onStarted?.Invoke(activity);
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        Source.Dispose();
    }
}
