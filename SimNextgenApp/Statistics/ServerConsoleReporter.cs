using Microsoft.Extensions.Logging;
using SimNextgenApp.Core;

namespace SimNextgenApp.Statistics;

public class ServerConsoleReporter<TLoad> : IReporter
{
    private readonly ServerObserver<TLoad> _observer;
    private readonly Server<TLoad> _server;
    private readonly ILogger _logger;

    public ServerConsoleReporter(Server<TLoad> server, ServerObserver<TLoad> observer, ILogger logger)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Report()
    {
        var busyServerMetric = _observer.BusyUnitsMetric;

        _logger.LogInformation("\n--- Server Stats ({ServerName}) ---", _server.Name);
        _logger.LogInformation($"Capacity: {_server.Capacity}");
        _logger.LogInformation($"Loads Completed (post-warmup): {_observer.LoadsCompleted}");
        _logger.LogInformation($"Loads Currently In Service: {_server.NumberInService}");
        _logger.LogInformation($"Server Utilization (post-warmup): {_observer.Utilization:P2}");
        _logger.LogInformation($"  Avg Busy Servers (post-warmup): {busyServerMetric.AverageCount:F3}");

        _logger.LogInformation("\n--- Detailed Server Busy Units Metric (post-warmup) ---");
        _logger.LogInformation($"Total Increments (servers becoming busy): {busyServerMetric.TotalIncrementObserved}");
        _logger.LogInformation($"Total Decrements (servers becoming free): {busyServerMetric.TotalDecrementObserved}");
        _logger.LogInformation($"Decrement Rate (Throughput): {busyServerMetric.DecrementRate:F4} per unit time");
        _logger.LogInformation($"Estimated Avg Sojourn Time (Little's Law): {busyServerMetric.AverageSojournTime:F3}");

        try
        {
            _logger.LogInformation($"Count at 95th Percentile by Time: {busyServerMetric.GetCountPercentileByTime(95)}");
            _logger.LogInformation($"Count at 50th Percentile by Time: {busyServerMetric.GetCountPercentileByTime(50)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting percentiles from TimeBasedMetric.");
        }

        ReportTimePerCount(busyServerMetric);
        ReportHistogram(busyServerMetric);
    }

    private void ReportTimePerCount(TimeBasedMetric busyServerMetric)
    {
        _logger.LogInformation("\nTime spent at each server count level (post-warmup):");
        if (busyServerMetric.TimePerCount.Any())
        {
            double totalDuration = busyServerMetric.TotalActiveDuration;
            if (totalDuration == 0) totalDuration = 1; // Avoid division by zero

            foreach (var kvp in busyServerMetric.TimePerCount.OrderBy(k => k.Key))
            {
                _logger.LogInformation($"  Count {kvp.Key}: {kvp.Value:F2} time units ({kvp.Value / totalDuration:P2})");
            }
        }
        else
        {
            _logger.LogInformation("  No data for time per count.");
        }
    }

    private void ReportHistogram(TimeBasedMetric busyServerMetric)
    {
        _logger.LogInformation("\nHistogram of Server Busy Units (Interval Width 1, post-warmup):");
        try
        {
            var histogram = busyServerMetric.GenerateHistogram(1.0);
            if (histogram.Any())
            {
                _logger.LogInformation("  Bin_Lower | Total_Time | Probability | Cumulative_Probability");
                _logger.LogInformation("  -------------------------------------------------------------");
                foreach (var bin in histogram)
                {
                    _logger.LogInformation($"  {bin.CountLowerBound,-9:F1} | {bin.TotalTime,-10:F2} | {bin.Probability,-11:P2} | {bin.CumulativeProbability,-10:P2}");
                }
            }
            else
            {
                _logger.LogInformation("  No data for histogram.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating histogram from TimeBasedMetric.");
        }
    }
}
