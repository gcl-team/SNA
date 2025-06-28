namespace SimNextgenApp.Events;

internal interface IServiceCompleter<TLoad>
{
    void HandleServiceCompletion(TLoad load, double currentTime);
}
