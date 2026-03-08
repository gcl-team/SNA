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

        // 2. Validate TimeUnit Precision (OPTIONAL but recommended)
        logger.LogInformation("\n--- Validating TimeUnit Precision ---");
        var targetTimeUnit = SimulationTimeUnit.Milliseconds;

        var validation = SimulationProfileValidator.ValidateTimeUnit(
            targetTimeUnit,
            new Dictionary<string, Func<Random, TimeSpan>>
            {
                ["Customer inter-arrival time"] = customerInterArrivalTime,
                ["Kitchen service time"] = (rnd) => serviceTimeFunc(null!, rnd)
            },
            sampleSize: 1000,
            truncationThreshold: 0.05
        );

        SimulationProfileValidator.LogValidationResult(validation, logger);

        if (!validation.IsValid)
        {
            logger.LogWarning($"TIP: Switching to {validation.RecommendedUnit} will prevent precision loss.");
            targetTimeUnit = validation.RecommendedUnit; // Auto-switch to recommended unit
        }

        // 3. Create the Main Model with the validated time unit
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
            loggerFactory,
            targetTimeUnit
            );

        // 4. Create Run Strategy and Profile
        // Use validated timeUnit for sub-second precision
        var timeUnit = targetTimeUnit;
        var duration = TimeSpan.FromMinutes(60 * 8);
        long durationInUnits = TimeUnitConverter.ConvertToSimulationUnits(duration, timeUnit);
        var runStrategy = new DurationRunStrategy(durationInUnits);
        var profile = new SimulationProfile(
            model: restaurantModel,
            runStrategy: runStrategy,
            timeUnit: timeUnit,
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
        // Average customer waiting time for a table (stored in simulation units)
        var avgWaitTime = restaurantModel.CustomerWaitTimesForTable.Count > 0 ? restaurantModel.CustomerWaitTimesForTable.Average() : 0.0;
        var avgWaitTimeSpan = TimeUnitConverter.ConvertFromSimulationUnits((long)avgWaitTime, timeUnit);
        var avgWaitTimeSeconds = avgWaitTimeSpan.TotalSeconds;
        logger.LogInformation($"Avg. Customer Wait Time for Table: {avgWaitTimeSeconds:F2} seconds");

        // Average queue length for orders in the kitchen
        var kitchenQueue = restaurantModel.OrderQueueForKitchen;
        logger.LogInformation($"Avg. Kitchen Order Queue Length: {kitchenQueue.TimeBasedMetric.AverageCount:F3} orders");

        // --- Service Time Statistics ---
        logger.LogInformation("\n--- Service Times ---");
        // Average time from order placement to food delivery (stored in simulation units)
        var avgOrderToDeliveryTime = restaurantModel.OrderToDeliveryTimes.Count > 0 ? restaurantModel.OrderToDeliveryTimes.Average() : 0.0;
        var avgOrderToDeliveryTimeSpan = TimeUnitConverter.ConvertFromSimulationUnits((long)avgOrderToDeliveryTime, timeUnit);
        var avgOrderToDeliverySeconds = avgOrderToDeliveryTimeSpan.TotalSeconds;
        logger.LogInformation($"Avg. Time from Order to Delivery: {avgOrderToDeliverySeconds:F2} seconds");
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
