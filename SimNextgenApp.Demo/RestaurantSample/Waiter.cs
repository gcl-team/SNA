using System.Drawing;

namespace SimNextgenApp.Demo.RestaurantSample;

internal class Waiter(int id, string name, Point currentLocation) : Staff(id, name)
{
    public required Point CurrentLocation { get; set; } = currentLocation;
}
