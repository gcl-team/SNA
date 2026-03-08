namespace SimNextgenApp.Modeling.Queue;

internal interface IOperatableQueue<TLoad>
{
    void HandleEnqueue(TLoad load, long currentTime);
    void HandleDequeue(long currentTime);
    void HandleUpdateToDequeue(bool toDequeue, long currentTime);
}