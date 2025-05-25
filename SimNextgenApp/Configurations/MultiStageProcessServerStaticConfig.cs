using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SimNextgenApp.Configurations;

/// <summary>
/// Represents the static configuration settings for a server that processes loads
/// through a sequence of one or more distinct processing stages.
/// </summary>
/// <typeparam name="TLoad">The type of load (entity) that this server will process.</typeparam>
public record MultiStageProcessServerStaticConfig<TLoad> : IStaticConfig
{
    /// <summary>
    /// Gets the immutable list of configurations for each sequential stage in the process.
    /// The order of configurations in this list defines the order of processing stages.
    /// This list must contain at least one stage configuration.
    /// </summary>
    /// <remarks>
    /// Each element in the list is a <see cref="ServerStaticConfig{TLoad}"/>,
    /// allowing individual configuration (like service time and capacity) for each stage.
    /// </remarks>
    public ImmutableList<ServerStaticConfig<TLoad>> StageConfigs { get; init; }

    /// <summary>
    /// Initialises a new instance of the <see cref="MultiStageProcessServerStaticConfig{TLoad}"/> record.
    /// </summary>
    /// <param name="stageConfigs">
    /// A collection of server configurations, one for each stage.
    /// The order of elements in this collection determines the processing sequence.
    /// The collection cannot be null and must contain at least one stage configuration.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stageConfigs"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="stageConfigs"/> is empty or contains any null elements.</exception>
    public MultiStageProcessServerStaticConfig(IEnumerable<ServerStaticConfig<TLoad>> stageConfigs)
    {
        ArgumentNullException.ThrowIfNull(stageConfigs);

        var stagesImmutableList = stageConfigs.ToImmutableList();

        if (stagesImmutableList.IsEmpty)
        {
            throw new ArgumentException("At least one stage configuration must be provided.", nameof(stageConfigs));
        }

        if (stagesImmutableList.Any(s => s == null))
        {
            throw new ArgumentException("Stage configurations cannot contain null elements.", nameof(stageConfigs));
        }

        StageConfigs = stagesImmutableList;
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="MultiStageProcessServerStaticConfig{TLoad}"/> record
    /// for a common two-stage process, providing a convenient way to define such configurations.
    /// </summary>
    /// <param name="stage1Time">The function defining service time for the first stage. Cannot be null.</param>
    /// <param name="stage2Time">The function defining service time for the second stage. Cannot be null.</param>
    /// <param name="stage1Capacity">Capacity for the first stage. Defaults to unlimited.</param>
    /// <param name="stage2Capacity">Capacity for the second stage. Defaults to unlimited.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="stage1Time"/> or <paramref name="stage2Time"/> is <c>null</c>.
    /// </exception>
    public MultiStageProcessServerStaticConfig(
        Func<TLoad, Random, TimeSpan> stage1Time,
        Func<TLoad, Random, TimeSpan> stage2Time,
        int stage1Capacity = int.MaxValue,
        int stage2Capacity = int.MaxValue)
        : this(CreateTwoStageList(stage1Time, stage2Time, stage1Capacity, stage2Capacity))
    {
    }

    /// <summary>
    /// Helper method to create the list for the two-stage constructor.
    /// This is extracted to keep the constructor chaining clean and allow ArgumentNullException to propagate correctly.
    /// </summary>
    private static List<ServerStaticConfig<TLoad>> CreateTwoStageList(
        Func<TLoad, Random, TimeSpan> stage1Time,
        Func<TLoad, Random, TimeSpan> stage2Time,
        int stage1Capacity,
        int stage2Capacity)
    {
        ArgumentNullException.ThrowIfNull(stage1Time);
        ArgumentNullException.ThrowIfNull(stage2Time);

        return
        [
            new ServerStaticConfig<TLoad>(stage1Time) { Capacity = stage1Capacity },
            new ServerStaticConfig<TLoad>(stage2Time) { Capacity = stage2Capacity }
        ];
    }
}