namespace SimNextgenApp.Demo.RestaurantSample;

internal class Order(CustomerGroup group, Table table, TimeSpan prepTime, double submittedTime)
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public CustomerGroup ForGroup { get; } = group;
    public Table AtTable { get; } = table;
    public double TimeSubmitted { get; } = submittedTime;
    public TimeSpan TotalPreparationTime { get; } = prepTime;
    public Chef? CookingChef { get; set; } = null;

    private static int _nextId = 0;
}
