namespace SimNextgenApp.Demo.RestaurantSample;

internal class CustomerGroup(int size)
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public int GroupSize { get; } = size;
    public double ArrivalTime { get; set; }

    private static int _nextId = 0;
}
