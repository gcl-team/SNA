namespace SimNextgenApp.Modeling.Server;

internal interface IOperatableServer<TLoad>
{
    void HandleServiceCompletion(TLoad load, double currentTime);
}
