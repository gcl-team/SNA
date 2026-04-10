using SimNextgenApp.Demo.CustomModels;

namespace SimNextgenApp.Demo.AzureDbSample;

/// <summary>
/// Represents a PostgreSQL query with connection pooling metadata.
/// Used to simulate PgBouncer-style connection pooling overhead.
/// </summary>
internal class PostgresQuery : MyLoad
{
    /// <summary>
    /// Indicates whether this query requires a new connection to be established.
    /// True = new connection (50ms overhead), False = reused connection
    /// </summary>
    public bool IsNewConnection { get; set; }

    /// <summary>
    /// The connection ID assigned to this query (for tracking pool usage).
    /// Null when using deferred acquisition (assigned at service start).
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>
    /// The pooling mode used for this query.
    /// </summary>
    public PoolingMode PoolMode { get; set; }
}

/// <summary>
/// PostgreSQL connection pooling modes (PgBouncer-style).
/// </summary>
internal enum PoolingMode
{
    /// <summary>
    /// Direct mode: New connection per query (50ms overhead every query).
    /// </summary>
    Direct,

    /// <summary>
    /// Session pooling: Connection held for session, reused with no overhead.
    /// Best for most workloads.
    /// </summary>
    SessionPooling,

    /// <summary>
    /// Transaction pooling: Connection released after transaction with state reset.
    /// Adds 8ms DISCARD ALL overhead per query.
    /// </summary>
    TransactionPooling
}
