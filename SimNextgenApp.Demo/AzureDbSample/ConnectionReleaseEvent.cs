using SimNextgenApp.Core;
using SimNextgenApp.Events;

namespace SimNextgenApp.Demo.AzureDbSample;

/// <summary>
/// Event for delayed connection release in session pooling mode.
/// Simulates client holding connection between queries in a session.
/// </summary>
internal class ConnectionReleaseEvent : AbstractEvent
{
    private readonly ConnectionPool _pool;
    private readonly string _queryId;

    public ConnectionReleaseEvent(ConnectionPool pool, string queryId)
    {
        _pool = pool;
        _queryId = queryId;
    }

    public override void Execute(IRunContext context)
    {
        // Release connection back to pool after session hold time
        _pool.ReleaseConnection(_queryId);
    }
}
