using System.Diagnostics;

namespace SimNextgenApp.Observability.Sampling;

/// <summary>
/// Defines the sampling strategy for OpenTelemetry traces.
/// Sampling reduces trace volume in large-scale simulations to prevent cost explosions.
/// </summary>
public enum SamplingStrategy
{
    /// <summary>
    /// Sample all traces (no sampling). This is the default.
    /// </summary>
    AlwaysOn,

    /// <summary>
    /// Never sample traces.
    /// </summary>
    AlwaysOff,

    /// <summary>
    /// Sample a random percentage of traces based on the configured rate.
    /// </summary>
    Random,

    /// <summary>
    /// Sample based on the parent span's sampling decision.
    /// If there is no parent, fall back to random sampling.
    /// </summary>
    ParentBased
}

/// <summary>
/// Configuration for trace sampling in OpenTelemetry.
/// Allows controlling the volume of telemetry data exported to backends.
/// </summary>
public class SamplingConfiguration
{
    /// <summary>
    /// Gets the sampling strategy to use.
    /// </summary>
    public SamplingStrategy Strategy { get; }

    /// <summary>
    /// Gets the sampling rate (0.0 to 1.0) for random sampling.
    /// 0.0 = sample nothing, 1.0 = sample everything.
    /// Only applicable when Strategy is Random or ParentBased.
    /// </summary>
    public double SamplingRate { get; }

    /// <summary>
    /// Gets a value indicating whether this configuration will actually sample traces.
    /// </summary>
    public bool IsEnabled => Strategy != SamplingStrategy.AlwaysOff;

    private SamplingConfiguration(SamplingStrategy strategy, double samplingRate)
    {
        Strategy = strategy;
        SamplingRate = samplingRate;
    }

    /// <summary>
    /// Creates a configuration that samples all traces (default behavior).
    /// </summary>
    public static SamplingConfiguration AlwaysOn() => new(SamplingStrategy.AlwaysOn, 1.0);

    /// <summary>
    /// Creates a configuration that never samples traces.
    /// </summary>
    public static SamplingConfiguration AlwaysOff() => new(SamplingStrategy.AlwaysOff, 0.0);

    /// <summary>
    /// Creates a configuration that randomly samples a percentage of traces.
    /// </summary>
    /// <param name="rate">Sampling rate between 0.0 (0%) and 1.0 (100%).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when rate is not between 0.0 and 1.0.</exception>
    public static SamplingConfiguration Random(double rate)
    {
        if (rate < 0.0 || rate > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Sampling rate must be between 0.0 and 1.0.");
        }

        return new(SamplingStrategy.Random, rate);
    }

    /// <summary>
    /// Creates a configuration that uses parent-based sampling with a fallback random rate.
    /// </summary>
    /// <param name="fallbackRate">Fallback sampling rate when there is no parent span.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when fallbackRate is not between 0.0 and 1.0.</exception>
    public static SamplingConfiguration ParentBased(double fallbackRate = 1.0)
    {
        if (fallbackRate < 0.0 || fallbackRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(fallbackRate), "Fallback sampling rate must be between 0.0 and 1.0.");
        }

        return new(SamplingStrategy.ParentBased, fallbackRate);
    }

    /// <summary>
    /// Determines whether a trace should be sampled based on the current configuration.
    /// This is a lightweight helper for manual sampling decisions.
    /// </summary>
    /// <returns>True if the trace should be sampled, false otherwise.</returns>
    public bool ShouldSample()
    {
        return Strategy switch
        {
            SamplingStrategy.AlwaysOn => true,
            SamplingStrategy.AlwaysOff => false,
            SamplingStrategy.Random => System.Random.Shared.NextDouble() < SamplingRate,
            SamplingStrategy.ParentBased => ShouldSampleParentBased(),
            _ => true
        };
    }

    private bool ShouldSampleParentBased()
    {
        var currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            // Use current activity's sampling decision (it's the parent of the span about to be created)
            return currentActivity.Recorded;
        }

        // No parent, fall back to random sampling
        return System.Random.Shared.NextDouble() < SamplingRate;
    }

    /// <summary>
    /// Returns a string representation of this sampling configuration.
    /// </summary>
    public override string ToString()
    {
        return Strategy switch
        {
            SamplingStrategy.AlwaysOn => "AlwaysOn (100%)",
            SamplingStrategy.AlwaysOff => "AlwaysOff (0%)",
            SamplingStrategy.Random => $"Random ({SamplingRate * 100:F0}%)",
            SamplingStrategy.ParentBased => $"ParentBased (fallback: {SamplingRate * 100:F0}%)",
            _ => Strategy.ToString()
        };
    }
}
