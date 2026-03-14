using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Observability.Tracing;
using SimNextgenApp.Observability.Metrics;
using SimNextgenApp.Observability.Reporting;

namespace SimNextgenApp.Demo.Scenarios;

internal static class SimpleServerAndGenerator
{
    public static void RunDemo(ILoggerFactory loggerFactory, double meanArrivalSeconds) 
    {
        var programLogger = loggerFactory.CreateLogger("Program");

        // 1. Generator Configuration
        Func<Random, TimeSpan> interArrivalTimeFunc = (rnd) =>
        {
            return TimeSpan.FromSeconds(-meanArrivalSeconds * Math.Log(1.0 - rnd.NextDouble()));
        };
        Func<Random, MyLoad> loadFactoryFunc = (rnd) => new MyLoad();
        var generatorConfig = new GeneratorStaticConfig<MyLoad>(interArrivalTimeFunc, loadFactoryFunc)
        {
            IsSkippingFirst = false
        };

        // 2. Server Configuration
        int serverCapacity = 1; // Single server
        Func<MyLoad, Random, TimeSpan> serviceTimeFunc = (load, rnd) =>
        {
            double meanServiceSeconds = 8.0;
            return TimeSpan.FromSeconds(-meanServiceSeconds * Math.Log(1.0 - rnd.NextDouble()));
        };

        // Create ServerStaticConfig using its constructor and then set Capacity if not infinite
        var serverConfig = new ServerStaticConfig<MyLoad>(serviceTimeFunc)
        {
            Capacity = serverCapacity
        };

        // 2.5. Validate TimeUnit Precision (OPTIONAL but recommended)
        // This validation helper checks if the chosen TimeUnit will cause precision issues
        programLogger.LogInformation("\n--- Validating TimeUnit Precision ---");
        var targetTimeUnit = SimulationTimeUnit.Milliseconds; // Use milliseconds for sub-second precision

        var validation = SimulationProfileValidator.ValidateTimeUnit(
            targetTimeUnit,
            new Dictionary<string, Func<Random, TimeSpan>>
            {
                ["Inter-arrival time"] = interArrivalTimeFunc,
                ["Service time"] = (rnd) => serviceTimeFunc(null!, rnd) // Wrap to match signature
            },
            sampleSize: 1000,
            truncationThreshold: 0.05 // Warn if >5% of samples truncate to 0
        );

        SimulationProfileValidator.LogValidationResult(validation, programLogger);

        if (!validation.IsValid)
        {
            programLogger.LogWarning($"TIP: Switching to {validation.RecommendedUnit} will prevent precision loss.");
            // User can choose to continue anyway or switch to recommended unit
            targetTimeUnit = validation.RecommendedUnit; // Auto-switch to recommended unit
        }

        // 3. Create the Composite Model
        var simpleSystem = new GeneratorAndServerModel(
            generatorConfig, 123,
            serverConfig, 456,
            loggerFactory);

        // 4. Create a Run Strategy
        // Convert durations from user-friendly seconds to simulation units
        double runDurationSeconds = 100.0;
        double warmupDurationSeconds = 20.0;

        long runDuration = TimeUnitConverter.ConvertToSimulationUnits(
            TimeSpan.FromSeconds(runDurationSeconds),
            targetTimeUnit
        );
        long warmupDuration = TimeUnitConverter.ConvertToSimulationUnits(
            TimeSpan.FromSeconds(warmupDurationSeconds),
            targetTimeUnit
        );

        var runStrategy = new DurationRunStrategy(runDuration, warmupDuration);

        // 5. Create a Memory Tracer to capture simulation events
        //    The MemoryTracer records every event that is scheduled and executed.
        var tracer = new MemoryTracer();

        // 6. Create the Simulation Profile to bundle all settings for a reproducible run
        //    This is useful for managing complex simulations with many parameters.
        var simulationProfile = new SimulationProfile(
            model: simpleSystem,
            runStrategy: runStrategy,
            "Simple Server and Generator Profile",
            targetTimeUnit, // Use the validated (and possibly auto-corrected) time unit
            loggerFactory: loggerFactory,
            tracer: tracer
        );

        // 7. Create and run the Simulation Engine
        var simulationEngine = new SimulationEngine(simulationProfile);

        SimulationResult? simulationResult = null;
        programLogger.LogInformation($"Starting simulation run for {runDuration} units, warmup {warmupDuration} units...");
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

        programLogger.LogInformation("\n--- Generator Stats ---");
        programLogger.LogInformation($"Loads Generated by '{simpleSystem.LoadGenerator.Name}': {simpleSystem.LoadGenerator.LoadsGeneratedCount}");

        var serverReporter = new ServerConsoleReporter<MyLoad>(
            simpleSystem.ServicePoint,
            simpleSystem.ServicePointObserver,
            programLogger,
            targetTimeUnit); // Use the same unit as the simulation
        serverReporter.Report();

        programLogger.LogInformation($"\n--- System Stats ---");
        programLogger.LogInformation($"Total Balked Loads (post-warmup): {simpleSystem.BalkedLoadsCount}");

        Console.WriteLine("\n--- Events recorded by MemoryTracer ---");
        tracer.PrintToConsole();
    }
}