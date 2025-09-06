namespace SimNextgenApp.Demo.RestaurantSample;

internal class Chef(int id, string name, double efficiency = 1.0) : Staff(id, name)
{
    public double EfficiencyMultiplier { get; } = efficiency;
}
