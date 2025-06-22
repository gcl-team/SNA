using Microsoft.Extensions.Logging;
using SimNextgenApp.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimNextgenApp.Events;

/// <summary>
/// Represents an abstract event associated with a server of type <typeparamref name="TLoad"/>.
/// </summary>
/// <typeparam name="TLoad">The type of load handled by the server associated with this event.</typeparam>
internal abstract class AbstractServerEvent<TLoad> : AbstractEvent
{
    internal Server<TLoad> OwningServer { get; }

    /// <inheritdoc/>
    public override IDictionary<string, object>? GetTraceDetails()
    {
        return new Dictionary<string, object>
        {
            { "GeneratorName", OwningServer.Name },
            { "Capacity", OwningServer.Capacity },
            { "Vacancy", OwningServer.Vacancy },
            { "NumberInService", OwningServer.NumberInService },
            { "LoadsCompletedCount", OwningServer.LoadsCompletedCount }
        };
    }

    protected AbstractServerEvent(Server<TLoad> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        OwningServer = owner;
    }
}

/// <summary>
/// Event representing a load starting service at the server.
/// </summary>
internal sealed class ServerStartServiceEvent<TLoad> : AbstractServerEvent<TLoad>
{
    public TLoad LoadToServe { get; }

    public ServerStartServiceEvent(Server<TLoad> owner, TLoad loadToServe) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(loadToServe);
        LoadToServe = loadToServe;
    }

    public override void Execute(IRunContext engine)
    {
        OwningServer.HandleLoadArrivalForService(LoadToServe, engine.ClockTime);
    }

    public override string ToString() => $"{OwningServer.Name}_StartService({LoadToServe})#{EventId} @ {ExecutionTime:F4}";
}

/// <summary>
/// Event representing a load completing service at the server.
/// </summary>
internal sealed class ServerServiceCompleteEvent<TLoad> : AbstractServerEvent<TLoad>
{
    public TLoad ServedLoad { get; }

    public ServerServiceCompleteEvent(Server<TLoad> owner, TLoad servedLoad) : base(owner)
    {
        ArgumentNullException.ThrowIfNull(servedLoad);
        ServedLoad = servedLoad;
    }

    public override void Execute(IRunContext engine)
    {
        OwningServer.HandleServiceCompletion(ServedLoad, engine.ClockTime);
    }

    public override string ToString() => $"{OwningServer.Name}_ServiceComplete({ServedLoad})#{EventId} @ {ExecutionTime:F4}";
}