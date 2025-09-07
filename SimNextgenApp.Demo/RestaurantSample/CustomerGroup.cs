namespace SimNextgenApp.Demo.RestaurantSample;

internal class CustomerGroup(int size, double arrivalTime)
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public int GroupSize { get; } = size;
    public double ArrivalTime { get; set; } = arrivalTime;

    private static int _nextId = 0;
}
