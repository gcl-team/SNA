namespace SimNextgenApp.Demo.RestaurantSample;

internal class CustomerGroup(int size, long arrivalTime)
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public int GroupSize { get; } = size;
    public long ArrivalTime { get; set; } = arrivalTime;
    private static int _nextId = 0;
}
