namespace SimNextgenApp.Core;

/// <summary>
/// Whether the simulation running on the current thread is still in its warmup period.
/// </summary>
/// <remarks>
/// <para>
/// Observers use this to label their metrics. They only see the component they watch, and component
/// events carry no run context, so there is no way to pass the warmup state to them explicitly.
/// </para>
/// <para>
/// <see cref="SimulationEngine.Run"/> sets it for the length of the run and restores the previous
/// value afterwards. A run executes on a single thread, so a thread-static field is enough, keeps
/// concurrent runs on other threads separate, and costs nothing per event. Outside a run it is false.
/// </para>
/// </remarks>
internal static class WarmupPhase
{
    [ThreadStatic]
    private static bool _isActive;

    /// <summary>
    /// Gets or sets whether the current thread's simulation is still in its warmup period.
    /// </summary>
    internal static bool IsActive
    {
        get => _isActive;
        set => _isActive = value;
    }
}
