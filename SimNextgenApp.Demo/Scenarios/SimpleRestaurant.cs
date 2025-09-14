using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Demo.RestaurantSample;
using SimNextgenApp.Statistics;
using System.Drawing;

namespace SimNextgenApp.Demo.Scenarios;

internal class SimpleRestaurant
{
    public static void RunDemo(ILoggerFactory loggerFactory,
        List<Table> availableTables,
        List<Waiter> availableWaiters,
        Point entranceLocation,
        Point kitchenLocation,
        Func<Random, TimeSpan> customerInterArrivalTime,
        Func<Random, CustomerGroup> customerFactory)
    {
        var logger = loggerFactory.CreateLogger("SimpleRestaurant");

        // 1. Configure Components
        Func<Point, Point, TimeSpan> walkTimeCalc = (p1, p2) =>
        {
            var distance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
            return TimeSpan.FromSeconds(distance / 1.5); // 1.5 meters/sec walking speed
        };

        // Customer arrival rate and group size distribution
        var genCustomerGroupConfig = new GeneratorStaticConfig<CustomerGroup>(customerInterArrivalTime, customerFactory)
        {
            IsSkippingFirst = false
        };
        var queueCustomerGroupConfig = new QueueStaticConfig<CustomerGroup>();
        var queueForKitchenConfig = new QueueStaticConfig<Order>();
        var queueForPickupConfig = new QueueStaticConfig<Order>();
        Func<Order, Random, TimeSpan> serviceTimeFunc = (load, rnd) =>
            TimeSpan.FromSeconds(-20 * Math.Log(1.0 - rnd.NextDouble()));
        var serverKitchenConfig = new ServerStaticConfig<Order>(serviceTimeFunc) { Capacity = 100 };

        // Customers browse the menu for 1 to 4 minutes (60 to 240 seconds)
        Func<Random, TimeSpan> menuBrowseTimeFunc = rnd =>
            TimeSpan.FromSeconds(Uniform(60, 240, rnd));

        // Customers eat for 15 to 30 minutes (900 to 1800 seconds)
        Func<Random, TimeSpan> eatingTimeFunc = rnd =>
            TimeSpan.FromSeconds(Uniform(900, 1800, rnd));

        // 2. Create the Main Model
        var restaurantModel = new RestaurantModel(
            walkTimeCalc,
            menuBrowseTimeFunc, eatingTimeFunc, 100,
            genCustomerGroupConfig, 100,
            queueCustomerGroupConfig,
            queueForKitchenConfig,
            queueForPickupConfig,
            availableWaiters,
            availableTables,
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
        var simulationEngine = new SimulationEngine(profile);
        
        SimulationResult? simulationResult = null;
        logger.LogInformation($"Starting simulation run...");
        try
        {
            simulationResult = simulationEngine.Run();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Simulation run failed!");
        }

        // 6. Report results and diagnostics
        logger.LogInformation($"\n--- Simulation Finished --- {simulationResult}");

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

    public static int SampleGeometricCustomerGroupSize(Random rnd, double p)
    {
        // Generate Uniform(0,1)
        double u = rnd.NextDouble();

        // Inverse CDF of geometric
        int k = (int)Math.Ceiling(Math.Log(1 - u) / Math.Log(1 - p));

        // Optional: cap at maxGroupSize
        return Math.Min(k, 5);
    }

    private static double Uniform(double min, double max, Random rnd)
    {
        return min + (max - min) * rnd.NextDouble();
    }
}
