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

/// <summary>
/// Simulates PostgreSQL connection pooling strategies on Azure B-series instances.
/// Compares Direct, Session, and Transaction pooling modes.
/// </summary>
internal static class AzurePgsqlPoolingScenario
{
    public static void RunDemo(
        ILoggerFactory loggerFactory,
        double runDuration,
        AzureDbBehavior dbBehavior,
        PoolingMode poolMode,
        int poolSize,
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
                var tempLogger = loggerFactory.CreateLogger("Azure-Pgsql-Pooling");
                tempLogger.LogError("GRAFANA_API_KEY environment variable not set!");
                tempLogger.LogWarning("Continuing simulation without Grafana export...");
            }
            else
            {
                try
                {
                    telemetry = SimulationTelemetry.Create()
                        .WithServiceInfo("Azure-Pgsql-Pooling", "1.0.0")
                        .WithOtlpExporter(
                            OtlpBackend.GrafanaCloud,
                            apiKey: grafanaApiKey,
                            region: grafanaRegion
                        )
                        .WithLogging(includeConsoleExporter: false, includeOtlpExporter: true)
                        .Build();

                    // Connect the database behavior to emit metrics
                    dbBehavior.SetMeter(telemetry.Meter);

                    // Use the OpenTelemetry-configured logger factory
                    if (telemetry.LoggerFactory != null)
                    {
                        activeLoggerFactory = telemetry.LoggerFactory;
                    }
                }
                catch (Exception ex)
                {
                    var tempLogger = loggerFactory.CreateLogger("Azure-Pgsql-Pooling");
                    tempLogger.LogError(ex, "Failed to configure Grafana Cloud export");
                    tempLogger.LogWarning("Continuing simulation without Grafana export...");
                }
            }
        }

        // Create two separate loggers for proper lifecycle management
        var programLogger = activeLoggerFactory.CreateLogger("Azure-Pgsql-Pooling");
        var cleanupLogger = loggerFactory.CreateLogger("Azure-Pgsql-Pooling-Cleanup");

        programLogger.LogInformation("--- Preparing Azure PostgreSQL Pooling Simulation ---");
        programLogger.LogInformation($"Pooling Mode: {poolMode}");
        programLogger.LogInformation($"Pool Size: {(poolMode == PoolingMode.Direct ? "N/A (Direct)" : poolSize.ToString())}");

        if (telemetry != null)
        {
            programLogger.LogInformation("Grafana Cloud OpenTelemetry export enabled!");
        }

        // Create connection pool (if not direct mode)
        ConnectionPool? pool = poolMode != PoolingMode.Direct ? new ConnectionPool(poolSize) : null;

        // Configure Generator with PostgreSQL query creation logic
        // High traffic: 50 req/sec (0.02s inter-arrival) to test connection overhead impact
        Func<Random, TimeSpan> interArrivalFunc = (rnd) =>
            TimeSpan.FromSeconds(-0.02 * Math.Log(1.0 - rnd.NextDouble()));

        Func<Random, MyLoad> createLoad = (rnd) =>
        {
            // DEFERRED ACQUISITION: Connection will be acquired when service starts,
            // not at load creation. This matches real PgBouncer behavior where
            // requests queue for connections.
            var query = new PostgresQuery
            {
                PoolMode = poolMode,
                ConnectionId = null, // ← Not assigned yet (deferred)
                IsNewConnection = false // ← Will be determined at service start
            };

            return query;
        };

        var generatorConfig = new GeneratorStaticConfig<MyLoad>(
            interArrivalFunc,
            createLoad
        );

        // Configure Server (using PostgreSQL-aware AzureDbBehavior)
        var serverConfig = new ServerStaticConfig<MyLoad>(dbBehavior.GetServiceTime)
        {
            Capacity = 1
        };

        var queueConfig = new QueueStaticConfig<MyLoad>();

        // Create Model
        var model = new SimpleMmckModel(
            generatorConfig,
            genSeed,
            queueConfig,
            serverConfig,
            numberOfServers: dbBehavior.Spec.VCores,
            serverSeedBase: 100,
            systemCapacityK: 50,
            activeLoggerFactory
        );

        // Create Engine with time unit validation
        var timeUnit = SimulationTimeUnit.Seconds;

        long runDurationInUnits = TimeUnitConverter.ConvertToSimulationUnits(
            TimeSpan.FromSeconds(runDuration),
            timeUnit
        );

        var profile = new SimulationProfile(
            model,
            new DurationRunStrategy(runDurationInUnits, null),
            "Azure PostgreSQL Pooling Simulation",
            timeUnit,
            activeLoggerFactory
        );

        var engine = new SimulationEngine(profile);

        // Validate TimeUnit Precision
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
                "Azure PostgreSQL Pooling Simulation",
                timeUnit,
                activeLoggerFactory
            );

            engine = new SimulationEngine(profile);
        }

        // Connect physics to engine
        dbBehavior.SetContext(engine);

        // Set connection pool for deferred acquisition (if using pooling mode)
        dbBehavior.SetConnectionPool(pool);

        // Release connections back to pool when service completes
        if (pool != null && poolMode != PoolingMode.Direct)
        {
            // Random number generator for session hold time
            var holdTimeRandom = new Random(genSeed + 999);

            foreach (var server in model.ServiceChannels)
            {
                server.LoadDeparted += (load, departureTime) =>
                {
                    if (load is PostgresQuery query)
                    {
                        if (poolMode == PoolingMode.SessionPooling)
                        {
                            // SESSION POOLING: Hold connection for random time (simulates client session)
                            // Client might run more queries on the same connection before releasing
                            // Mean hold time: 100ms (typical think time between queries in a session)
                            double holdTimeSecs = -0.1 * Math.Log(1.0 - holdTimeRandom.NextDouble());
                            long holdTimeUnits = TimeUnitConverter.ConvertToSimulationUnits(
                                TimeSpan.FromSeconds(holdTimeSecs),
                                engine.TimeUnit
                            );

                            // Schedule delayed release event
                            long releaseTime = departureTime + holdTimeUnits;
                            var releaseEvent = new ConnectionReleaseEvent(pool, query.Id.ToString());
                            engine.Schedule(releaseEvent, releaseTime);
                        }
                        else
                        {
                            // TRANSACTION POOLING: Release immediately
                            // Connection returned to pool right away for next transaction
                            pool.ReleaseConnection(query.Id.ToString());
                        }
                    }
                };
            }
        }

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
            // Flush telemetry data
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
                    cleanupLogger.LogInformation("Telemetry disposed successfully");
                }
                catch (Exception ex)
                {
                    cleanupLogger.LogError(ex, "Telemetry disposal failed");
                }
            }

            dbBehavior.FinalizeExport("output");

            cleanupLogger.LogInformation("Simulation Complete.");
            cleanupLogger.LogInformation($"Check 'output/' directory for CSV results.");
        }
    }
}
