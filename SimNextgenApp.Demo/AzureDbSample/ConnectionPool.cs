namespace SimNextgenApp.Demo.AzureDbSample;

/// <summary>
/// Simulates a PgBouncer-style connection pool for PostgreSQL.
/// Manages connection lifecycle for pooling modes.
/// </summary>
internal class ConnectionPool
{
    private readonly int _poolSize;
    private readonly HashSet<string> _availableConnections;
    private readonly Dictionary<string, string> _assignedConnections; // Query → Connection

    public ConnectionPool(int poolSize)
    {
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
    /// </summary>
    public string? AcquireConnection(string queryId)
    {
        if (_availableConnections.Count == 0)
        {
            // Pool exhausted - query must wait
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
