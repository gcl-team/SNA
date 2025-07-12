using SimNextgenApp.Core;
using SimNextgenApp.Modeling.Server;

namespace SimNextgenApp.Events;

/// <summary>
/// Represents an abstract event associated with a server of type <typeparamref name="TLoad"/>.
/// </summary>
/// <typeparam name="TLoad">The type of load handled by the server associated with this event.</typeparam>
internal abstract class AbstractServerEvent<TLoad> : AbstractEvent where TLoad : notnull
{
    internal IServer<TLoad> OwningServer { get; }

    /// <inheritdoc/>
    public override IDictionary<string, object>? GetTraceDetails()
    {
        return new Dictionary<string, object>
        {
            { "ServerName", OwningServer.Name },
            { "Capacity", OwningServer.Capacity },
            { "Vacancy", OwningServer.Vacancy },
            { "NumberInService", OwningServer.NumberInService }
        };
    }

    protected AbstractServerEvent(Server<TLoad> owner)
    {
        OwningServer = owner ?? throw new ArgumentNullException(nameof(owner));
    }
}

/// <summary>
/// Event representing a load completing service at the server.
/// </summary>
internal sealed class ServerServiceCompleteEvent<TLoad> : AbstractServerEvent<TLoad> where TLoad : notnull
{
    /// <summary>
    /// Gets the load currently being served by the system.
    /// </summary>
    public TLoad ServedLoad { get; }

    /// <summary>
    /// Initialises a new instance of the <see cref="ServerServiceCompleteEvent{TLoad}"/> class, representing the
    /// completion of a service operation on a server.
    /// </summary>
    /// <param name="owner">The server instance that completed the service operation.</param>
    /// <param name="servedLoad">The load that was served during the operation. Cannot be <see langword="null"/>.</param>
    public ServerServiceCompleteEvent(Server<TLoad> owner, TLoad servedLoad) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(servedLoad);
        ServedLoad = servedLoad;
    }

    /// <inheritdoc/>
    public override void Execute(IRunContext engine)
    {
        if (OwningServer is IOperatableServer<TLoad> completer)
        {
            completer.HandleServiceCompletion(ServedLoad, engine.ClockTime);
        }
        else
        {
            throw new InvalidOperationException($"The server '{OwningServer.Name}' does not implement IOperatableServer and cannot handle this event.");
        }
    }

    /// <inheritdoc/>
    public override string ToString() => $"{OwningServer.Name}_ServiceComplete({ServedLoad})#{EventId} @ {ExecutionTime:F4}";
}