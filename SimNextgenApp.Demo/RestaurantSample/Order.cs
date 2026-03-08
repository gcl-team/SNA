namespace SimNextgenApp.Demo.RestaurantSample;

internal class Order(CustomerGroup group, Table table, TimeSpan prepTime, long submittedTime)
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public CustomerGroup ForGroup { get; } = group;
    public Table AtTable { get; } = table;
    public long TimeSubmitted { get; } = submittedTime;
    public TimeSpan TotalPreparationTime { get; } = prepTime;

    private static int _nextId = 0;
}
