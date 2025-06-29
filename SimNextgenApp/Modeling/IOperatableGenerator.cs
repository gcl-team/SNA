namespace SimNextgenApp.Modeling;

internal interface IOperatableGenerator<TLoad>
{
    void HandleActivation(double currentTime);
    void HandleDeactivation();
    void HandleLoadGeneration(double currentTime);
}
