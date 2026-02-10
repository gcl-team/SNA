using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Demo.AwsT3Sample;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Statistics;

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
        var profile = new SimulationProfile(
            model, 
            new DurationRunStrategy(runDuration, 0), 
            "AWS T3 Crash", 
            SimulationTimeUnit.Seconds, 
            loggerFactory, 
            new MemoryTracer()
        );

        var engine = new SimulationEngine(profile);

        // =========================================================
        // 6. THE CRITICAL STEP: CONNECT PHYSICS TO TIME
        // =========================================================
        // We inject the engine into our physics object so it can read ClockTime
        t3Physics.SetContext(engine); 
        // =========================================================

        programLogger.LogInformation("Starting Simulation. Watch console for CSV output...");
        
        if (!Directory.Exists("./output")) Directory.CreateDirectory("./output");
        if (File.Exists("./output/simulation_latency.csv")) File.Delete("./output/simulation_latency.csv");
        if (File.Exists("./output/simulation_credits.csv")) File.Delete("./output/simulation_credits.csv");
        File.WriteAllText("./output/simulation_latency.csv", "Time (s),Latency (ms)\n");
        File.WriteAllText("./output/simulation_credits.csv", "Time (s),Credits\n");

        engine.Run();

        programLogger.LogInformation("Simulation Complete.");
    }
}