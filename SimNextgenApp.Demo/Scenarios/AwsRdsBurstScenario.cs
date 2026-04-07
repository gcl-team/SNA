using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Core.Strategies;
using SimNextgenApp.Core.Utilities;
using SimNextgenApp.Demo.AwsRdsSample;
using SimNextgenApp.Demo.CustomModels;
using SimNextgenApp.Observability;
using SimNextgenApp.Observability.Exporters;

namespace SimNextgenApp.Demo.Scenarios;

internal static class AwsBurstScenario
{
    public static void RunDemo(
        ILoggerFactory loggerFactory,
        double runDuration,
        AwsRdsBehavior rdsBehavior,
        int genSeed,
        bool enableGrafana = false)
    {
        var programLogger = loggerFactory.CreateLogger("AWS-Simulation");
        programLogger.LogInformation("--- Preparing AWS RDS Burst Simulation ---");

        // Setup OpenTelemetry if Grafana export is enabled
        SimulationTelemetry? telemetry = null;
        if (enableGrafana)
        {
            programLogger.LogInformation("Grafana Cloud export enabled. Configuring OpenTelemetry...");

            // Read credentials from environment variables
            // Format: GRAFANA_API_KEY="<stack_id>:<token>"
            // Example: GRAFANA_API_KEY="123456:glc_..."
            var grafanaApiKey = Environment.GetEnvironmentVariable("GRAFANA_API_KEY");
            var grafanaRegion = Environment.GetEnvironmentVariable("GRAFANA_REGION") ?? "us-central-0";

            if (string.IsNullOrEmpty(grafanaApiKey))
            {
                programLogger.LogError("GRAFANA_API_KEY environment variable not set!");
                programLogger.LogError("Please set: export GRAFANA_API_KEY=\"<stack_id>:<token>\"");
                programLogger.LogWarning("Continuing simulation without Grafana export...");
                return;
            }

            try
            {
                // Create OTLP config to inspect endpoint
                var otlpConfig = OtlpExporterConfiguration.ConfigureForBackend(
                    OtlpBackend.GrafanaCloud,
                    apiKey: grafanaApiKey,
                    region: grafanaRegion);

                programLogger.LogInformation($"[DEBUG] OTLP Endpoint: {otlpConfig.MetricsEndpoint}");
                programLogger.LogInformation($"[DEBUG] Auth Header: {otlpConfig.Headers["Authorization"].Substring(0, 20)}...");

                // Console.WriteLine("DEBUGGING OTLP EXPORT - ALL DETAILS BELOW:");
                // Console.WriteLine($"   API Key: {grafanaApiKey}");
                // Console.WriteLine($"   Region: {grafanaRegion}");
                // Console.WriteLine($"   Expected Endpoint: https://otlp-gateway-prod-{grafanaRegion}.grafana.net/otlp");

                telemetry = SimulationTelemetry.Create()
                    .WithServiceInfo("AWS-RDS-Simulation", "1.0.0")
                    .WithOtlpExporter(
                        OtlpBackend.GrafanaCloud,
                        apiKey: grafanaApiKey,
                        region: grafanaRegion
                    )
                    .WithLogging(includeConsoleExporter: false, includeOtlpExporter: true)
                    .Build();

                // Connect the RDS behavior to emit metrics
                rdsBehavior.SetMeter(telemetry.Meter);

                programLogger.LogInformation($"OpenTelemetry configured for Grafana Cloud (region: {grafanaRegion})");
                programLogger.LogInformation($"Using API key starting with: {grafanaApiKey.Substring(0, Math.Min(15, grafanaApiKey.Length))}...");
            }
            catch (Exception ex)
            {
                programLogger.LogError($"Failed to configure Grafana Cloud export: {ex.Message}");
                programLogger.LogWarning("Continuing simulation without Grafana export...");
            }
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
        // 6. THE CRITICAL STEP: CONNECT PHYSICS TO ENGINE
        // =========================================================
        // We inject the engine context so the behavior can access ClockTime and TimeUnit
        rdsBehavior.SetContext(engine);
        // =========================================================

        programLogger.LogInformation("Starting Simulation. Watch console for CSV output...");

        // Observe the simulation if telemetry is enabled
        SimulationObserver? simObserver = null;
        if (telemetry != null)
        {
            simObserver = telemetry.ObserveSimulation(engine);
        }

        engine.Run();

        // Flush telemetry data to ensure all metrics are sent
        if (telemetry != null)
        {
            programLogger.LogInformation("Flushing metrics to Grafana Cloud...");

            try
            {
                programLogger.LogInformation("Calling Flush()...");
                telemetry.Flush();

                // Extra aggressive: Sleep to let background threads finish
                programLogger.LogInformation("Sleeping 5 seconds to ensure export completes...");
                System.Threading.Thread.Sleep(5000);
                programLogger.LogInformation("Flush completed - waited 5s for background export");
            }
            catch (Exception ex)
            {
                programLogger.LogError($"FLUSH FAILED: {ex.Message}");
                programLogger.LogError($"Stack trace: {ex.StackTrace}");
            }

            try
            {
                simObserver?.Dispose();
                telemetry.Dispose();
                programLogger.LogInformation("Telemetry disposed successfully");
            }
            catch (Exception ex)
            {
                programLogger.LogError($"DISPOSE FAILED: {ex.Message}");
            }
        }

        rdsBehavior.FinalizeExport("output");

        programLogger.LogInformation("Simulation Complete.");
    }
}