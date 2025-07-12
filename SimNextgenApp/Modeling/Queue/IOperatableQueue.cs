namespace SimNextgenApp.Modeling.Queue;

internal interface IOperatableQueue<TLoad>
{
    void HandleEnqueue(TLoad load, double currentTime);
    void HandleDequeue(double currentTime);
    void HandleUpdateToDequeue(bool toDequeue, double currentTime);
}