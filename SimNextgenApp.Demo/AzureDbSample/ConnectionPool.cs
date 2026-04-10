namespace SimNextgenApp.Demo.AzureDbSample;

/// <summary>
/// Simulates a PgBouncer-style connection pool for PostgreSQL with HARD LIMIT semantics.
/// Connection acquisition happens when server starts processing (deferred acquisition),
/// matching real PgBouncer behavior where requests queue for available connections.
/// </summary>
internal class ConnectionPool
{
    private readonly int _poolSize;
    private readonly HashSet<string> _availableConnections;
    private readonly Dictionary<string, string> _assignedConnections; // Query → Connection

    public ConnectionPool(int poolSize)
    {
        if (poolSize <= 0)
        {
            throw new ArgumentException($"Pool size must be positive (got {poolSize}). Use Direct mode if you don't want pooling.", nameof(poolSize));
        }

        _poolSize = poolSize;
        _availableConnections = new HashSet<string>();
        _assignedConnections = new Dictionary<string, string>();

        // Initialize pool with connection IDs
        for (int i = 0; i < poolSize; i++)
        {
            _availableConnections.Add($"conn_{i}");
        }
    }

    /// <summary>
    /// Attempts to acquire a connection from the pool.
    /// Returns connection ID if successful, null if pool is exhausted.
    ///
    /// SPILLOVER MODEL: When pool is exhausted (returns null), caller opens
    /// a new direct connection to the database, bypassing the pool and paying
    /// full connection overhead (50ms). This models scenarios where applications
    /// fall back to direct connections when the pool is saturated, allowing
    /// temporary exceedance of the pool size under load spikes.
    ///
    /// Note: This differs from PgBouncer's default queue-and-wait behavior.
    /// Use this model to simulate spillover capacity in high-traffic scenarios.
    ///
    /// Called at SERVICE START (deferred acquisition), not at load creation.
    /// Requests naturally queue in SimQueue before reaching this point.
    /// </summary>
    public string? AcquireConnection(string queryId)
    {
        if (_availableConnections.Count == 0)
        {
            // Pool exhausted - caller opens direct connection bypassing pool (spillover model)
            return null;
        }

        // Get first available connection
        string connectionId = _availableConnections.First();
        _availableConnections.Remove(connectionId);
        _assignedConnections[queryId] = connectionId;

        return connectionId;
    }

    /// <summary>
    /// Releases a connection back to the pool after query completion.
    /// </summary>
    public void ReleaseConnection(string queryId)
    {
        if (_assignedConnections.TryGetValue(queryId, out string? connectionId))
        {
            _assignedConnections.Remove(queryId);
            _availableConnections.Add(connectionId);
        }
    }

    /// <summary>
    /// Total capacity of the connection pool.
    /// </summary>
    public int Capacity => _poolSize;

    /// <summary>
    /// Number of available connections in the pool.
    /// </summary>
    public int AvailableCount => _availableConnections.Count;

    /// <summary>
    /// Number of connections currently in use.
    /// </summary>
    public int InUseCount => _assignedConnections.Count;

    /// <summary>
    /// Indicates whether the pool is fully exhausted (no available connections).
    /// </summary>
    public bool IsExhausted => _availableConnections.Count == 0;
}
