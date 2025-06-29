namespace SimNextgenApp.Modeling;

internal interface IOperatableServer<TLoad>
{
    void HandleServiceCompletion(TLoad load, double currentTime);
}
