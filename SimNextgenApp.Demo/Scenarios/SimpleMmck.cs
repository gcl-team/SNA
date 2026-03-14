using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Observability.Logs;
using SimNextgenApp.Observability.Metrics;
using SimNextgenApp.Observability.Exporters;

namespace SimNextgenApp.Demo.Scenarios;

internal static class SimpleMmck
{
    public static void RunDemo(
        ILoggerFactory loggerFactory,
        int numberOfServers,
        int systemCapacityK,
        double meanArrivalSecs,
        double meanServiceSecs,
        double runDuration,
        double warmupDuration,
        int genSeed,
        int serverSeedBase
    )
    {
        var programLogger = loggerFactory.CreateLogger("Program");

        // 1. Generator Configuration
        Func<Random, TimeSpan> interArrivalTimeFunc = (rnd) =>
            TimeSpan.FromSeconds(-meanArrivalSecs * Math.Log(1.0 - rnd.NextDouble()));
        Func<Random, MyLoad> loadFactoryFunc = (rnd) => new MyLoad();
        var generatorConfig = new GeneratorStaticConfig<MyLoad>(interArrivalTimeFunc, loadFactoryFunc)
        { IsSkippingFirst = false };

        // 2. Queue Configuration (Capacity will be overridden by mmckSystem)
        var queueConfig = new QueueStaticConfig<MyLoad>(); // Uses default infinite, model will set K-c

        // 3. Server Configuration (Template for all servers)
        Func<MyLoad, Random, TimeSpan> serviceTimeFunc = (load, rnd) =>
            TimeSpan.FromSeconds(-meanServiceSecs * Math.Log(1.0 - rnd.NextDouble()));
        var serverConfigTemplate = new ServerStaticConfig<MyLoad>(serviceTimeFunc) { Capacity = 1 }; // Each server unit has capacity 1

        // 4. Create the Composite M/M/c/K Model
        var mmckSystem = new SimpleMmckModel(
            generatorConfig, genSeed,
            queueConfig,
            serverConfigTemplate, numberOfServers, serverSeedBase,
            systemCapacityK,
            loggerFactory);

        programLogger.LogInformation($"M/M/c/K System Created: c={mmckSystem.NumberOfServersC}, K={mmckSystem.SystemCapacityK}, Queue Capacity (K-c)={mmckSystem.WaitingLine.Capacity}");

        // 4.5. Validate TimeUnit Precision (OPTIONAL but recommended)
        programLogger.LogInformation("\n--- Validating TimeUnit Precision ---");
        var targetTimeUnit = SimulationTimeUnit.Milliseconds;

        var validation = SimulationProfileValidator.ValidateTimeUnit(
            targetTimeUnit,
            new Dictionary<string, Func<Random, TimeSpan>>
            {
                ["Inter-arrival time"] = interArrivalTimeFunc,
                ["Service time"] = (rnd) => serviceTimeFunc(null!, rnd)
            },
            sampleSize: 1000,
            truncationThreshold: 0.05
        );

        SimulationProfileValidator.LogValidationResult(validation, programLogger);

        if (!validation.IsValid)
        {
            programLogger.LogWarning($"TIP: Switching to {validation.RecommendedUnit} will prevent precision loss.");
            targetTimeUnit = validation.RecommendedUnit; // Auto-switch to recommended unit
        }

        // 5. Create a Run Strategy
        // Use validated timeUnit for sub-second precision
        var timeUnit = targetTimeUnit;

        long runDurationInUnits = TimeUnitConverter.ConvertToSimulationUnits(
            TimeSpan.FromSeconds(runDuration),
            timeUnit
        );
        long? warmupDurationInUnits = warmupDuration > 0
            ? TimeUnitConverter.ConvertToSimulationUnits(TimeSpan.FromSeconds(warmupDuration), timeUnit)
            : null;

        var runStrategy = new DurationRunStrategy(runDurationInUnits, warmupDurationInUnits);

        // 6. Create a Memory Tracer to capture simulation events
        //    The MemoryTracer records every event that is scheduled and executed.
        var tracer = new MemoryTracer();

        // 7. Create the Simulation Profile to bundle all settings for a reproducible run
        //    This is useful for managing complex simulations with many parameters.
        var simulationProfile = new SimulationProfile(
            model: mmckSystem,
            runStrategy: runStrategy,
            "M/M/c/K Profile",
            timeUnit, // Use Milliseconds for precision
            loggerFactory: loggerFactory,
            tracer: tracer
        );

        // 8. Create and run the Simulation Engine
        var simulationEngine = new SimulationEngine(simulationProfile);

        programLogger.LogInformation($"Starting M/M/c/K simulation run for {runDuration} units, warmup {warmupDuration} units...");
        SimulationResult? simulationResult = null;
        try
        {
            simulationResult = simulationEngine.Run();
        }
        catch (Exception ex)
        {
            programLogger.LogCritical(ex, "Simulation run failed!");
        }

        // 9. Report results and diagnostics
        programLogger.LogInformation($"\n--- Simulation Finished --- {simulationResult}");

        programLogger.LogInformation("\n--- Generator Stats (Post-Warmup) ---");
        programLogger.LogInformation($"Loads Generated by '{mmckSystem.LoadGenerator.Name}': {mmckSystem.LoadGenerator.LoadsGeneratedCount}");

        programLogger.LogInformation("\n--- System Stats (Post-Warmup) ---");
        programLogger.LogInformation($"Total Loads Entered System: {mmckSystem.TotalLoadsEnteredSystem}");
        programLogger.LogInformation($"Total Balked Loads (System Full): {mmckSystem.BalkedLoadsCount}");

        programLogger.LogInformation("\n--- Queue Stats ('{QueueName}', Post-Warmup) ---", mmckSystem.WaitingLine.Name);
        var queueMetric = mmckSystem.WaitingLine.TimeBasedMetric;
        programLogger.LogInformation($"Capacity (Waiting Spots): {mmckSystem.WaitingLine.Capacity}");
        programLogger.LogInformation($"Final Occupancy: {mmckSystem.WaitingLine.Occupancy}");
        programLogger.LogInformation($"Average Queue Length: {queueMetric.AverageCount:F3}");
        programLogger.LogInformation($"Max Queue Length Observed (from history if enabled, or need separate tracker)");


        programLogger.LogInformation("\n--- Server Stats (Aggregated for {NumServers} Servers, Post-Warmup) ---", mmckSystem.NumberOfServersC);

        for (int s = 0; s < mmckSystem.ServiceChannels.Count; s++)
        {
            var serverReporter = new ServerConsoleReporter<MyLoad>(
                mmckSystem.ServiceChannels[s],
                mmckSystem.ServiceChannelObservers[s],
                programLogger,
                timeUnit); // Use the same unit as the simulation
            serverReporter.Report();
        }

        Console.WriteLine("\n--- Events recorded by MemoryTracer ---");
        tracer.PrintToConsole();
    }
}
