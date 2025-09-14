using System.Drawing;

namespace SimNextgenApp.Demo.RestaurantSample;

internal class Table(int id, int capacity, Point location)
{
    public int Id { get; } = id;
    public int SeatCapacity { get; } = capacity;
    public Point Location { get; } = location;
}
