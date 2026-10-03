using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace SimNextgenApp.Benchmarks;

/// <summary>
/// Accepts every span batch and drops it. Used behind a <see cref="BatchActivityExportProcessor"/>
/// to measure the SDK export path without network I/O or unbounded in-memory growth.
/// </summary>
internal sealed class DiscardingActivityExporter : BaseExporter<Activity>
{
    private long _exportedCount;

    /// <summary>Total spans handed to this exporter, used to spot spans dropped by a full queue.</summary>
    public long ExportedCount => Interlocked.Read(ref _exportedCount);

    public override ExportResult Export(in Batch<Activity> batch)
    {
        Interlocked.Add(ref _exportedCount, batch.Count);
        return ExportResult.Success;
    }
}

/// <summary>
/// Accepts every metric batch and drops it.
/// </summary>
internal sealed class DiscardingMetricExporter : BaseExporter<Metric>
{
    public override ExportResult Export(in Batch<Metric> batch) => ExportResult.Success;
}
