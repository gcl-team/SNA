using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.AwsRdsSample;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Observability.Exporters;

namespace SimNextgenApp.Demo.Scenarios;

internal static class AwsBurstScenario
{
    public static void RunDemo(
        ILoggerFactory loggerFactory,
        double runDuration,
        AwsRdsBehavior rdsBehavior,
        int genSeed)
    {
        var programLogger = loggerFactory.CreateLogger("AWS-Simulation");
        programLogger.LogInformation("--- Preparing AWS RDS Burst Simulation ---");

        // 2. Configure Generator (High Load)
        // High traffic: 20 req/sec (0.05s inter-arrival) to drain the credits
        Func<Random, TimeSpan> interArrivalFunc = (rnd) =>
            TimeSpan.FromSeconds(-0.05 * Math.Log(1.0 - rnd.NextDouble()));
            
        var generatorConfig = new GeneratorStaticConfig<MyLoad>(
            interArrivalFunc, 
            (rnd) => new MyLoad()
        );

        // 3. Configure Server (Using the Physics Class)
        // WE PASS THE METHOD from the rdsBehavior instance
        var serverConfig = new ServerStaticConfig<MyLoad>(rdsBehavior.GetServiceTime) 
        { 
            Capacity = 1 
        };

        var queueConfig = new QueueStaticConfig<MyLoad>();

        // 4. Create Model
        // Assuming SimpleMmckModel exists from your previous code
        var model = new SimpleMmckModel(
            generatorConfig, 
            genSeed,
            queueConfig,
            serverConfig, 
            numberOfServers: 2, // db.t3.medium has 2 vCPUs
            serverSeedBase: 100,
            systemCapacityK: 50,
            loggerFactory
        );

        // 5. Create Engine
        // Start with Seconds (validation will auto-correct if needed)
        var timeUnit = SimulationTimeUnit.Seconds;

        long runDurationInUnits = TimeUnitConverter.ConvertToSimulationUnits(
            TimeSpan.FromSeconds(runDuration),
            timeUnit
        );

        var profile = new SimulationProfile(
            model,
            new DurationRunStrategy(runDurationInUnits, null),
            "AWS RDS Burstable Simulation",
            timeUnit,
            loggerFactory
        );

        var engine = new SimulationEngine(profile);

        // 5.5. Validate TimeUnit Precision (OPTIONAL but recommended)
        programLogger.LogInformation("\n--- Validating TimeUnit Precision ---");
        var validation = SimulationProfileValidator.ValidateTimeUnit(
            timeUnit,
            new Dictionary<string, Func<Random, TimeSpan>>
            {
                ["Inter-arrival time"] = interArrivalFunc
            },
            sampleSize: 1000,
            truncationThreshold: 0.05
        );

        SimulationProfileValidator.LogValidationResult(validation, programLogger);

        if (!validation.IsValid)
        {
            programLogger.LogWarning($"TIP: Auto-switching from {timeUnit} to {validation.RecommendedUnit} to prevent precision loss.");

            // Re-create profile with recommended unit
            timeUnit = validation.RecommendedUnit;
            runDurationInUnits = TimeUnitConverter.ConvertToSimulationUnits(
                TimeSpan.FromSeconds(runDuration),
                timeUnit
            );

            profile = new SimulationProfile(
                model,
                new DurationRunStrategy(runDurationInUnits, null),
                "AWS RDS Burstable Simulation",
                timeUnit,
                loggerFactory
            );

            engine = new SimulationEngine(profile);
        }

        // =========================================================
        // 6. THE CRITICAL STEP: CONNECT PHYSICS TO TIME
        // =========================================================
        // We inject the engine into our physics object so it can read ClockTime
        // and tell it what time unit is being used so it can convert to seconds
        rdsBehavior.SetContext(engine, timeUnit);
        // =========================================================

        programLogger.LogInformation("Starting Simulation. Watch console for CSV output...");
        
        engine.Run();

        rdsBehavior.FinalizeExport("output");

        programLogger.LogInformation("Simulation Complete.");
    }
}