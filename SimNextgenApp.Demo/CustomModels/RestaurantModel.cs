using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Demo.RestaurantSample;
using SimNextgenApp.Modeling;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Modeling.Queue;
using SimNextgenApp.Modeling.Resource;
using SimNextgenApp.Modeling.Server;
using System.Drawing;

namespace SimNextgenApp.Demo.CustomModels;

internal class RestaurantModel : AbstractSimulationModel
{
    private IRunContext _runContext = null!;
    private readonly ILoggerFactory _loggerFactory;

    // --- Components ---
    public Generator<CustomerGroup> CustomerArrivals { get; }
    public SimQueue<CustomerGroup> WaitingForTableQueue { get; }

    // Resources
    public ResourcePool<Waiter> Waiters { get; }
    public Server<Order> KitchenServer { get; }
    public TableManager TableManager { get; }

    // Queues for workflow
    public SimQueue<Order> OrderQueueForKitchen { get; }
    public SimQueue<Order> CookedFoodQueueForPickup { get; }

    // --- Configuration ---
    // Function to calculate walk time based on two points
    private readonly Func<Point, Point, TimeSpan> _walkTimeCalculator;
    private readonly Point entranceLocation = new(0, 0);

    public RestaurantModel(
        GeneratorStaticConfig<CustomerGroup> genCustomerGroupConfig, int genCustomerGroupSeed,
        QueueStaticConfig<CustomerGroup> queueCustomerGroupConfig,
        QueueStaticConfig<Order> queueForKitchenConfig,
        QueueStaticConfig<Order> queueForPickupConfig,
        IEnumerable<Waiter> waiters,
        IEnumerable<Table> tables,
        ServerStaticConfig<Order> serverKitchenConfig, int serverKitchenSeed,
        ILoggerFactory loggerFactory, string name = "Restaurant")
        : base(name)
    {
        _loggerFactory = loggerFactory;

        CustomerArrivals = new Generator<CustomerGroup>(genCustomerGroupConfig, genCustomerGroupSeed, $"{name}_CustomerGroup_Arrivals", loggerFactory);
        WaitingForTableQueue = new SimQueue<CustomerGroup>(queueCustomerGroupConfig, $"{name}_CustomerGroup_Queue", loggerFactory);

        Waiters = new ResourcePool<Waiter>(waiters, $"{name}_Waiter_ResourcePool", loggerFactory);
        KitchenServer = new Server<Order>(serverKitchenConfig, serverKitchenSeed, $"{name}_Kitchen_Server");
        TableManager = new TableManager(tables);

        OrderQueueForKitchen = new SimQueue<Order>(queueForKitchenConfig, $"{name}_ForKitchen_Queue", loggerFactory);
        CookedFoodQueueForPickup = new SimQueue<Order>(queueForPickupConfig, $"{name}_ForKitchen_Queue", loggerFactory);

        CustomerArrivals.LoadGenerated += (group, time) => TrySeatOrQueueNewCustomer(group);
    }

    public override void Initialize(IRunContext runContext)
    {
        _runContext = runContext;

        // Initialize all child components
        CustomerArrivals.Initialize(runContext);
        WaitingForTableQueue.Initialize(runContext);
        Waiters.Initialize(runContext);
        KitchenServer.Initialize(runContext);
        OrderQueueForKitchen.Initialize(runContext);
        CookedFoodQueueForPickup.Initialize(runContext);

        // Wire up events that need the runContext.
        // Customer Arrival Flow
        CustomerArrivals.LoadGenerated += (group, time) => TrySeatOrQueueNewCustomer(group); // Renamed for clarity
        WaitingForTableQueue.LoadDequeued += HandleWaitingCustomerDequeued; // CORRECTED

        // Waiter becomes free, check for work
        Waiters.ResourceReleased += Waiters_ResourceReleased;

        // Kitchen Flow
        KitchenServer.LoadDeparted += HandleCookingComplete; // This is correct
        OrderQueueForKitchen.LoadDequeued += (order, time) => KitchenServer.TryStartService(order, _runContext); // This is correct

        // Food Pickup Flow
        CookedFoodQueueForPickup.LoadDequeued += HandleFoodPickup;
    }

    private void Waiters_ResourceReleased(Waiter arg1, double arg2)
    {
        // Check 1: Any customer groups waiting for a table?
        if (WaitingForTableQueue.Occupancy > 0 && TableManager.AvailableTableCount > 0)
        {
            WaitingForTableQueue.TriggerDequeueAttempt(_runContext);
            return;
        }

        // Check 2: Any cooked food waiting for pickup?
        if (CookedFoodQueueForPickup.Occupancy > 0)
        {
            CookedFoodQueueForPickup.TriggerDequeueAttempt(_runContext);
            return;
        }
    }

    /// <summary>
    /// This method handles a NEW customer group that has just arrived at the restaurant.
    /// Its logic is to attempt an immediate seating, and if that fails,
    /// place the group into the waiting queue.
    /// </summary>
    internal void TrySeatOrQueueNewCustomer(CustomerGroup group)
    {
        Table? availableTable = TableManager.FindAvailableTable(group.GroupSize);

        Waiter? availableWaiter = Waiters.TryAcquire(_runContext);

        if (availableTable != null && availableWaiter != null)
        {
            // Success! We have a table and a waiter to seat them.
            TableManager.OccupyTable(availableTable, group);

            // Schedule the "Seating Complete" event after walking time.
            var walkTime = _walkTimeCalculator(entranceLocation, availableTable.Location);
            _runContext.Scheduler.Schedule(
                new SeatingCompleteEvent(this, group, availableTable, availableWaiter),
                walkTime
            );
        }
        else
        {
            // Failure. Either no table or no waiter.
            // Release any acquired resource.
            if (availableWaiter != null) Waiters.Release(availableWaiter, _runContext);

            // Put the customer group in the waiting queue.
            WaitingForTableQueue.TryScheduleEnqueue(group, _runContext);
        }
    }

    internal void HandleWaitingCustomerDequeued(CustomerGroup group, double arrivalTime)
    {
        Table? availableTable = TableManager.FindAvailableTable(group.GroupSize);

        Waiter? availableWaiter = Waiters.TryAcquire(_runContext);

        if (availableTable != null && availableWaiter != null)
        {
            // Success! We have a table and a waiter to seat them.
            TableManager.OccupyTable(availableTable, group);

            // Schedule the "Seating Complete" event after walking time.
            var walkTime = _walkTimeCalculator(entranceLocation, availableTable.Location);
            _runContext.Scheduler.Schedule(
                new SeatingCompleteEvent(this, group, availableTable, availableWaiter),
                walkTime
            );
        }
        else
        {
            // Failure. Either no table or no waiter.
            // Release any acquired resource.
            if (availableWaiter != null) Waiters.Release(availableWaiter, _runContext);

            // Put the customer group in the waiting queue.
            WaitingForTableQueue.TryScheduleEnqueue(group, _runContext);
        }
    }

    /// <summary>
    /// Defines what happens at the moment a customer group is officially seated at their table.
    /// </summary>
    internal void HandleSeatingComplete(CustomerGroup group, Table table, Waiter waiter, IRunContext context)
    {
        var logger = _loggerFactory.CreateLogger<RestaurantModel>();
        logger.LogInformation($"--- [SEATING COMPLETE] SimTime: {context.ClockTime:F2} -> Group {group.Id} seated at Table {table.Id} by {waiter.Name}.");

        // Step 1: Update the state of the resources used in the completed activity.
        // The waiter's task of seating is done, so they are now free.
        // Update the waiter's location to the table they just walked to.
        waiter.CurrentLocation = table.Location;
        Waiters.Release(waiter, context);
        logger.LogInformation($"      {waiter.Name} is now free and at Table {table.Id}'s location.");

        // Step 2: Schedule the next event in this customer group's journey.
        // After being seated, customers browse the menu for a period of time.
        // We schedule a "ReadyToOrderEvent" to occur in the future.
        context.Scheduler.Schedule(
            new ReadyToOrderEvent(this, group, table),
            TimeSpan.FromSeconds(20)
        );
    }

    internal void HandleReadyToOrder(CustomerGroup group, Table table, IRunContext context)
    {
        var logger = _loggerFactory.CreateLogger<RestaurantModel>();
        logger.LogInformation($"--- [READY TO ORDER] SimTime: {context.ClockTime:F2} -> Group {group.Id} seated at Table {table.Id}.");

        // Step 1: Submit the order to the kitchen queue.
        var order = new Order(group, table, TimeSpan.FromSeconds(20), context.ClockTime);
        OrderQueueForKitchen.TryScheduleEnqueue(order, context);

        if (KitchenServer.Vacancy > 0)
        {
            // If the kitchen has a free chef, start cooking immediately.
            OrderQueueForKitchen.TriggerDequeueAttempt(context);
        }
    }

    internal void HandleCookingComplete(Order order, double finishedCookingTime)
    {
        Waiter? availableWaiter = Waiters.TryAcquire(_runContext);

        if (availableWaiter != null)
        {
            // Success! We have a waiter to deliver the food.
            // Schedule the "Food Serving Complete" event after walking time.
            var table = order.AtTable;
            var walkTime = _walkTimeCalculator(availableWaiter.CurrentLocation, table.Location);
            _runContext.Scheduler.Schedule(
                new FoodServingCompleteEvent(this, order.ForGroup, table),
                walkTime
            );
        }
        else
        {
            // Failure. No waiter.

            // Put the food order in the waiting queue.
            CookedFoodQueueForPickup.TryScheduleEnqueue(order, _runContext);
        }

        // Since the cooking is done, the kitchen chef is now free.
        if (OrderQueueForKitchen.Occupancy > 0)
        {
            // If there are pending orders, the kitchen should start the next one.
            OrderQueueForKitchen.TriggerDequeueAttempt(_runContext);
        }
    }

    internal void HandleFoodPickup(Order order, double pickupTime)
    {
        // A waiter is free and there's food waiting.
        Waiter? availableWaiter = Waiters.TryAcquire(_runContext);

        if (availableWaiter != null)
        {
            var table = order.AtTable;
            var walkTime = _walkTimeCalculator(availableWaiter.CurrentLocation, table.Location);
            _runContext.Scheduler.Schedule(
                new FoodServingCompleteEvent(this, order.ForGroup, table),
                walkTime
            );
        }
        else
        {
            // Re-queue the order.
            CookedFoodQueueForPickup.TryScheduleEnqueue(order, _runContext);
        }
    }

    internal void HandleReadyToEat(CustomerGroup group, Table table, IRunContext context)
    {
        var logger = _loggerFactory.CreateLogger<RestaurantModel>();
        logger.LogInformation($"--- [READY TO EAT] SimTime: {context.ClockTime:F2} -> Group {group.Id} seated at Table {table.Id}.");

        // Step 1: Finish the eating activity after a fixed duration.
        context.Scheduler.Schedule(
            new EatingCompleteEvent(this, table),
            TimeSpan.FromSeconds(20)
        );
    }

    internal void HandleEatingComplete(Table table, IRunContext context)
    {
        var logger = _loggerFactory.CreateLogger<RestaurantModel>();
        logger.LogInformation($"--- [EATING COMPLETE] SimTime: {context.ClockTime:F2} -> Table {table.Id} is now free.");

        // Step 1: Free up the table.
        TableManager.ReleaseTable(table);

        // Step 2: Try to seat any waiting customer groups.
        if (WaitingForTableQueue.Occupancy > 0)
        {
            WaitingForTableQueue.TriggerDequeueAttempt(context);
        }
    }
}
