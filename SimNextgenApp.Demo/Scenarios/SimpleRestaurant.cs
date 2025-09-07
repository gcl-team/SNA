using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Demo.RestaurantSample;
using System.Drawing;

namespace SimNextgenApp.Demo.Scenarios;

internal class SimpleRestaurant
{
    public static void RunDemo(ILoggerFactory loggerFactory)
    {
        // 1. Define Restaurant Layout and Resources
        var tables = new List<Table>
        {
            new Table(1, 2, new Point(10, 10)),
            new Table(2, 4, new Point(10, 20)),
            new Table(3, 4, new Point(20, 20))
        };

        var waiterStartingPoint = new Point(0, 15);
        var waiters = Enumerable.Range(1, 3).Select(i => new Waiter(i, $"Waiter {i}", waiterStartingPoint)).ToList();

        // Location constants
        var entranceLocation = new Point(0, 15);
        var kitchenLocation = new Point(50, 15);

        Func<Point, Point, TimeSpan> walkTimeCalc = (p1, p2) =>
        {
            var distance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
            return TimeSpan.FromSeconds(distance / 1.5); // 1.5 meters/sec walking speed
        };

        // 2. Configure Components
        // Customer arrival rate and group size distribution
        Func<Random, TimeSpan> interArrivalTime = rnd => TimeSpan.FromMinutes(Exponential(5, rnd));
        Func<Random, CustomerGroup> customerFactory = rnd => new CustomerGroup(ChooseGroupSize(rnd), 0);
        var genCustomerGroupConfig = new GeneratorStaticConfig<CustomerGroup>(interArrivalTime, customerFactory)
        {
            IsSkippingFirst = false
        };
        var queueCustomerGroupConfig = new QueueStaticConfig<CustomerGroup>();
        var queueForKitchenConfig = new QueueStaticConfig<Order>();
        var queueForPickupConfig = new QueueStaticConfig<Order>();
        Func<Order, Random, TimeSpan> serviceTimeFunc = (load, rnd) =>
            TimeSpan.FromSeconds(-20 * Math.Log(1.0 - rnd.NextDouble()));
        var serverKitchenConfig = new ServerStaticConfig<Order>(serviceTimeFunc) { Capacity = 100 };

        // 3. Create the Main Model
        var restaurantModel = new RestaurantModel(
            walkTimeCalc,
            genCustomerGroupConfig, 100,
            queueCustomerGroupConfig,
            queueForKitchenConfig,
            queueForPickupConfig,
            waiters,
            tables,
            serverKitchenConfig, 100,
            loggerFactory
            );

        // 4. Create Run Strategy and Profile
        var runStrategy = new DurationRunStrategy(TimeSpan.FromMinutes(60 * 8).TotalSeconds);
        var profile = new SimulationProfile(
            model: restaurantModel,
            runStrategy: runStrategy,
            timeUnit: SimulationTimeUnit.Seconds,
            loggerFactory: loggerFactory
        );

        // 5. Create and Run the Engine
        var engine = new SimulationEngine(profile);
        var result = engine.Run();

        // 6. Report Statistics
        var logger = loggerFactory.CreateLogger("RestaurantReport");
        logger.LogInformation("\n--- SIMULATION COMPLETE ---");
        logger.LogInformation($"Final Simulation Time: {result.FinalClockTime:F2} seconds ({result.FinalClockTime / 3600:F2} hours)");
        logger.LogInformation($"Total Events Processed: {result.ExecutedEventCount}");

        // --- Waiter Utilization ---
        logger.LogInformation("\n--- Staff Utilization ---");
        var waiterPool = restaurantModel.Waiters;
        // Formula: (Average number of busy waiters / Total number of waiters) * 100
        var avgWaiterUtilization = (waiterPool.UtilizationMetric.AverageCount / waiterPool.TotalCapacity) * 100;
        logger.LogInformation($"Waiter Utilization: {avgWaiterUtilization:F2}% (Avg {waiterPool.UtilizationMetric.AverageCount:F2} of {waiterPool.TotalCapacity} busy)");

        // --- Table Utilization ---
        logger.LogInformation("\n--- Facility Utilization ---");
        var tableMgr = restaurantModel.TableManager;
        // Formula: (Average number of occupied tables / Total number of tables) * 100
        var avgTableUtilization = (tableMgr.UtilizationMetric.AverageCount / tableMgr.TotalTableCount) * 100;
        logger.LogInformation($"Table Utilization: {avgTableUtilization:F2}% (Avg {tableMgr.UtilizationMetric.AverageCount:F2} of {tableMgr.TotalTableCount} occupied)");

        // --- Queueing Statistics ---
        logger.LogInformation("\n--- Queue Statistics ---");
        // Average customer waiting time for a table
        var avgWaitTime = restaurantModel.CustomerWaitTimesForTable.Any() ? restaurantModel.CustomerWaitTimesForTable.Average() : 0.0;
        logger.LogInformation($"Avg. Customer Wait Time for Table: {avgWaitTime:F2} seconds");

        // Average queue length for orders in the kitchen
        var kitchenQueue = restaurantModel.OrderQueueForKitchen;
        logger.LogInformation($"Avg. Kitchen Order Queue Length: {kitchenQueue.TimeBasedMetric.AverageCount:F3} orders");

        // --- Service Time Statistics ---
        logger.LogInformation("\n--- Service Times ---");
        // Average time from order placement to food delivery
        var avgOrderToDelivery = restaurantModel.OrderToDeliveryTimes.Any() ? restaurantModel.OrderToDeliveryTimes.Average() : 0.0;
        logger.LogInformation($"Avg. Time from Order to Delivery: {avgOrderToDelivery:F2} seconds");
    }

    private static int ChooseGroupSize(Random rnd) 
    {
        return rnd.Next(1, 5);
    }

    private static double Exponential(double mean, Random rnd) 
    {
        return -mean * Math.Log(1.0 - rnd.NextDouble());
    }
}
