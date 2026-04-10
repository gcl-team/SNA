using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.AzureDbSample;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Observability;
using SimNextgenApp.Observability.Exporters;

namespace SimNextgenApp.Demo.Scenarios;

internal static class AzureDbBurstScenario
{
    public static void RunDemo(
        ILoggerFactory loggerFactory,
        double runDuration,
        AzureDbBehavior dbBehavior,
        int genSeed,
        bool enableGrafana = false)
    {
        SimulationTelemetry? telemetry = null;
        ILoggerFactory activeLoggerFactory = loggerFactory;

        if (enableGrafana)
        {
            var grafanaApiKey = Environment.GetEnvironmentVariable("GRAFANA_API_KEY");
            var grafanaRegion = Environment.GetEnvironmentVariable("GRAFANA_REGION") ?? "us-central-0";

            if (string.IsNullOrWhiteSpace(grafanaApiKey))
            {
                var tempLogger = loggerFactory.CreateLogger("Azure-DB-Simulation");
                tempLogger.LogError("GRAFANA_API_KEY environment variable not set!");
                tempLogger.LogWarning("Continuing simulation without Grafana export...");
            }
            else
            {
                try
                {
                    telemetry = SimulationTelemetry.Create()
                        .WithServiceInfo("Azure-DB-Simulation", "1.0.0")
                        .WithOtlpExporter(
                            OtlpBackend.GrafanaCloud,
                            apiKey: grafanaApiKey,
                            region: grafanaRegion
                        )
                        .WithLogging(includeConsoleExporter: false, includeOtlpExporter: true)
                        .Build();

                    // Connect the database behavior to emit metrics
                    dbBehavior.SetMeter(telemetry.Meter);

                    // Use the OpenTelemetry-configured logger factory!
                    if (telemetry.LoggerFactory != null)
                    {
                        activeLoggerFactory = telemetry.LoggerFactory;
                    }
                }
                catch (Exception ex)
                {
                    var tempLogger = loggerFactory.CreateLogger("Azure-DB-Simulation");
                    tempLogger.LogError(ex, "Failed to configure Grafana Cloud export");
                    tempLogger.LogWarning("Continuing simulation without Grafana export...");
                }
            }
        }

        // Create two separate loggers:
        // - programLogger: May use telemetry.LoggerFactory (for OTel log correlation)
        // - cleanupLogger: Always uses original loggerFactory (survives telemetry disposal)
        // This prevents use-after-dispose bugs when logging cleanup/disposal messages
        var programLogger = activeLoggerFactory.CreateLogger("Azure-DB-Simulation");
        var cleanupLogger = loggerFactory.CreateLogger("Azure-DB-Simulation-Cleanup");

        programLogger.LogInformation("--- Preparing Azure Database Burst Simulation ---");

        if (telemetry != null)
        {
            programLogger.LogInformation("Grafana Cloud OpenTelemetry export enabled!");
        }

        // 2. Configure Generator (High Load)
        // High traffic: 20 req/sec (0.05s inter-arrival) to drain the credits
        Func<Random, TimeSpan> interArrivalFunc = (rnd) =>
            TimeSpan.FromSeconds(-0.05 * Math.Log(1.0 - rnd.NextDouble()));

        var generatorConfig = new GeneratorStaticConfig<MyLoad>(
            interArrivalFunc,
            (rnd) => new MyLoad()
        );

        // 3. Configure Server (Using the Physics Class)
        // Pass the method from the dbBehavior instance
        var serverConfig = new ServerStaticConfig<MyLoad>(dbBehavior.GetServiceTime)
        {
            Capacity = 1
        };

        var queueConfig = new QueueStaticConfig<MyLoad>();

        // 4. Create Model
        var model = new SimpleMmckModel(
            generatorConfig,
            genSeed,
            queueConfig,
            serverConfig,
            numberOfServers: dbBehavior.Spec.VCores, // Use actual vCore count from behavior's spec
            serverSeedBase: 100,
            systemCapacityK: 50,
            activeLoggerFactory
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
            "Azure Database Burstable Simulation",
            timeUnit,
            activeLoggerFactory
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
                "Azure Database Burstable Simulation",
                timeUnit,
                activeLoggerFactory
            );

            engine = new SimulationEngine(profile);
        }

        // =========================================================
        // 6. THE CRITICAL STEP: CONNECT PHYSICS TO ENGINE
        // =========================================================
        // We inject the engine context so the behavior can access ClockTime and TimeUnit
        dbBehavior.SetContext(engine);
        // =========================================================

        programLogger.LogInformation("Starting Simulation. Watch console for CSV output...");

        // Observe the simulation if telemetry is enabled
        SimulationObserver? simObserver = null;
        if (telemetry != null)
        {
            simObserver = telemetry.ObserveSimulation(engine);
        }

        try
        {
            engine.Run();
        }
        finally
        {
            // Flush telemetry data to ensure all metrics are sent
            if (telemetry != null)
            {
                programLogger.LogInformation("Flushing metrics to Grafana Cloud...");

                try
                {
                    programLogger.LogInformation("Calling Flush() with 5s timeout...");
                    bool flushSuccess = telemetry.Flush(5000);

                    if (flushSuccess)
                    {
                        programLogger.LogInformation("Flush completed successfully");
                    }
                    else
                    {
                        programLogger.LogWarning("Flush timed out before all telemetry was exported");
                    }
                }
                catch (Exception ex)
                {
                    programLogger.LogError(ex, "FLUSH FAILED");
                }

                try
                {
                    simObserver?.Dispose();
                    telemetry.Dispose();
                    // Use cleanupLogger here because telemetry (and its LoggerFactory) is now disposed
                    cleanupLogger.LogInformation("Telemetry disposed successfully");
                }
                catch (Exception ex)
                {
                    // Use cleanupLogger for error logging during disposal
                    cleanupLogger.LogError(ex, "Telemetry disposal failed");
                }
            }

            dbBehavior.FinalizeExport("output");

            cleanupLogger.LogInformation("Simulation Complete.");
        }
    }
}
