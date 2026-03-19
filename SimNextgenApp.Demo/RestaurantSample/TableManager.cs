using Microsoft.Extensions.Logging;
using SimNextgenApp.Modeling.Resource;
using System.Diagnostics;
using System.Xml.Linq;

namespace SimNextgenApp.Demo.RestaurantSample;

internal class TableManager
{
    private readonly ILogger<TableManager> _logger;

    private readonly List<Table> _allTables;

    private readonly Dictionary<Table, CustomerGroup> _occupiedTables;

    // This event is CRUCIAL. It signals that a table has become free,
    // allowing the main model to check the waiting queue.
    public event Action<Table>? TableFreed;

    public int TotalTableCount => _allTables.Count;
    public int OccupiedTableCount => _occupiedTables.Count;
    public int AvailableTableCount => TotalTableCount - OccupiedTableCount;

    public TableManager(IEnumerable<Table> tables, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TableManager>();
        _allTables = [.. tables];
        _occupiedTables = [];
    }

    /// <summary>
    /// Finds the best available table for a group of a given size.
    /// "Best" is defined as the smallest table that can fit the group.
    /// This method ONLY FINDS the table, it does not occupy it.
    /// </summary>
    /// <param name="groupSize">The number of customers in the group.</param>
    /// <returns>The best-fit Table, or null if no suitable table is available.</returns>
    public Table? FindAvailableTable(int groupSize)
    {
        return _allTables
            .Where(t => !_occupiedTables.ContainsKey(t)) // Filter for available tables
            .Where(t => t.SeatCapacity >= groupSize)      // Filter for tables that are big enough
            .OrderBy(t => t.SeatCapacity)                 // Order by size to find the "best fit"
            .FirstOrDefault();                            // Select the first one, which is the smallest
    }

    /// <summary>
    /// Marks a table as occupied by a specific customer group.
    /// </summary>
    public void OccupyTable(Table table, CustomerGroup group, long currentTime)
    {
        if (_occupiedTables.ContainsKey(table))
        {
            throw new InvalidOperationException($"Attempted to occupy table {table.Id} which is already occupied.");
        }

        _occupiedTables[table] = group;
        _logger.LogTrace("Table acquired at {Time}. Customer Group Size: {CustomerGroupSize}; Table Available: {AvailableTable}", 
            currentTime, group.GroupSize, AvailableTableCount);
    }

    /// <summary>
    /// Releases a table, making it available for new customers.
    /// </summary>
    public void ReleaseTable(Table table, long currentTime)
    {
        if (_occupiedTables.Remove(table))
        {
            TableFreed?.Invoke(table);
            _logger.LogTrace("Table released at {Time}. Table Available: {AvailableTable}", currentTime, AvailableTableCount);
        }
    }

    public void WarmedUp(long simulationTime)
    {
    }
}
