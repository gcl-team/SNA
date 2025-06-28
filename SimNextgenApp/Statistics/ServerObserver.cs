using SimNextgenApp.Core;

namespace SimNextgenApp.Statistics;

/// <summary>
/// Observes and tracks the performance metrics of a server.
/// </summary>
/// <typeparam name="TLoad">The type of load being processed by the server.</typeparam>
public class ServerObserver<TLoad>
{
    private readonly Server<TLoad> _server;
    private readonly TimeBasedMetric _busyServerUnitsCounter;
    private int _loadsCompletedCount;

    /// <summary>
    /// Gets the total number of loads that have completed service
    /// during the observation period. This value is updated live.
    /// </summary>
    public int LoadsCompleted => _loadsCompletedCount;

    /// <summary>
    /// Gets the average server utilisation, calculated as the time-weighted
    /// average of busy server units divided by the server capacity.
    /// This property is calculated live and is always up-to-date with the latest observation.
    /// </summary>
    public double Utilization => _server.Capacity > 0 ? _busyServerUnitsCounter.AverageCount / _server.Capacity : 0.0;

    /// <summary>
    /// Provides access to the detailed time-based metrics for the server's busy units,
    /// allowing for advanced statistical analysis like percentiles and histograms.
    /// </summary>
    public TimeBasedMetric BusyUnitsMetric => _busyServerUnitsCounter;
    

    public ServerObserver(Server<TLoad> serverToObserve)
    {
        _server = serverToObserve ?? throw new ArgumentNullException(nameof(serverToObserve));

        _busyServerUnitsCounter = new TimeBasedMetric();
        _loadsCompletedCount = 0;

        // Subscribe to the server's events
        _server.StateChanged += OnServerStateChange;
        _server.LoadDeparted += OnLoadDepart;
    }

    /// <summary>
    /// Resets the observer's statistics. This should be called after a simulation
    /// warm-up period to clear transient data.
    /// </summary>
    public void WarmedUp(double simulationTime)
    {
        _busyServerUnitsCounter.WarmedUp(simulationTime);
        _loadsCompletedCount = 0;

        // Re-initialise the counter with the server current state.
        _busyServerUnitsCounter.ObserveCount(_server.NumberInService, simulationTime);
    }

    private void OnServerStateChange(double currentTime)
    {
        _busyServerUnitsCounter.ObserveCount(_server.NumberInService, currentTime);
    }

    private void OnLoadDepart(TLoad load, double currentTime)
    {
        _loadsCompletedCount++;
    }
}