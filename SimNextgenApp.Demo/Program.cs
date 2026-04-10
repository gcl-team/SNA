using Microsoft.Extensions.Logging;
using Serilog;
using SimNextgenApp.Demo.AwsRdsSample;
using SimNextgenApp.Demo.AzureDbSample;
using SimNextgenApp.Demo.RestaurantSample;
using SimNextgenApp.Demo.Scenarios;
using System.CommandLine;
using System.Drawing;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("SimNextgenApp", LogLevel.Trace)
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddConsole();
});

// Root command
var rootCommand = new RootCommand
{
    Description = "Discrete Event Simulation Demo CLI\n\n" +
                  "Use 'demo <subcommand> --help' to view options for a specific demo.\n\n" +
                  "Examples:\n" +
                  "  dotnet DemoApp.dll demo simple-generator\n" +
                  "  dotnet DemoApp.dll demo mmck --servers 3 --capacity 10 --arrival-secs 2.5"
};

// Show help when run with no arguments
if (args.Length == 0)
{
    Console.WriteLine("No command provided. Showing help:\n");
    rootCommand.Invoke("-h"); // Show help
    return 1;
}

// ---- Demo: simple-generator ----
var simpleGenCommand = new Command("simple-generator", "Run the SimpleGenerator demo");
simpleGenCommand.SetHandler(() =>
{
    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Seq("http://localhost:5341")
            .CreateLogger();

    // Create a logger factory that uses Serilog
    loggerFactory = new LoggerFactory().AddSerilog(Log.Logger);

    Console.WriteLine("====== Running SimpleGenerator ======");
    SimpleGenerator.RunDemo(loggerFactory);
});

// ---- Demo: simple-server ----
var meanArrivalSecondsOption = new Option<double>(
    name: "--arrival-secs",
    description: "Mean arrival time in seconds.",
    getDefaultValue: () => 5.0
);

var simpleServerCommand = new Command("simple-server", "Run the SimpleServerAndGenerator demo");
simpleServerCommand.AddOption(meanArrivalSecondsOption);

simpleServerCommand.SetHandler((double meanArrivalSeconds) =>
{
    Console.WriteLine($"====== Running SimpleServerAndGenerator (Mean Arrival (Unit: second)={meanArrivalSeconds}) ======");
    SimpleServerAndGenerator.RunDemo(loggerFactory, meanArrivalSeconds);
}, meanArrivalSecondsOption);

// ---- Demo: M/M/c/K ----
var mmckCommand = new Command("mmck", "Run the SimpleMmck demo");

// Define options
var serversOption = new Option<int>("--servers", () => 2, "Number of servers");
var capacityOption = new Option<int>("--capacity", () => 5, "System capacity (K)");
var arrivalSecsOption = new Option<double>("--arrival-secs", () => 3.0, "Mean seconds between arrivals");
var serviceSecsOption = new Option<double>("--service-secs", () => 5.0, "Mean service time per server (seconds)");
var durationOption = new Option<double>("--duration", () => 500.0, "Total simulation time");
var warmupOption = new Option<double>("--warmup", () => 100.0, "Warmup time before collecting stats");
var genSeedOption = new Option<int>("--gen-seed", () => 2024, "Random seed for generator");
var serverSeedBaseOption = new Option<int>("--server-seed-base", () => 100, "Seed base for all servers");

// Add options one-by-one
mmckCommand.AddOption(serversOption);
mmckCommand.AddOption(capacityOption);
mmckCommand.AddOption(arrivalSecsOption);
mmckCommand.AddOption(serviceSecsOption);
mmckCommand.AddOption(durationOption);
mmckCommand.AddOption(warmupOption);
mmckCommand.AddOption(genSeedOption);
mmckCommand.AddOption(serverSeedBaseOption);

// Set handler
mmckCommand.SetHandler(
    (int servers, int capacity, double arrivalSecs, double serviceSecs,
     double duration, double warmup, int genSeed, int serverSeedBase) =>
    {
        Console.WriteLine($"====== Running MMCK Demo (c={servers}, K={capacity}) ======");
        SimpleMmck.RunDemo(
            loggerFactory,
            servers, capacity, arrivalSecs, serviceSecs,
            duration, warmup, genSeed, serverSeedBase
        );
    },
    serversOption, capacityOption, arrivalSecsOption, serviceSecsOption,
    durationOption, warmupOption, genSeedOption, serverSeedBaseOption
);

// ---- Demo: simple-restaurant ----
var simpleRestaurantCommand = new Command("simple-restaurant", "Run the SimpleRestaurant demo");

// Define options
var tablesOption = new Option<List<Table>>(
    name: "--table",
    description: "Capacity and location of table (format: capacity,x,y)",
    parseArgument: result =>
    {
        var tables = new List<Table>();

        for (int i = 0; i < result.Tokens.Count; i++)
        {
            var token = result.Tokens[i];

            var parts = token.Value.Split(',');
            if (parts.Length != 3)
            {
                result.ErrorMessage =
                    $"Invalid format '{token.Value}'. Expected 'capacity,x,y'.";
                continue;
            }

            if (!int.TryParse(parts[0], out var capacity))
            {
                result.ErrorMessage = $"Invalid capacity in '{token.Value}'.";
                continue;
            }

            if (!int.TryParse(parts[1], out var x) || !int.TryParse(parts[2], out var y))
            {
                result.ErrorMessage = $"Invalid coordinates in '{token.Value}'.";
                continue;
            }

            tables.Add(new Table(i, capacity, new Point(x, y)));
        }

        return tables;
    })
{
    Arity = ArgumentArity.OneOrMore
};
var waitersOption = new Option<List<Waiter>>(
    name: "--waiter",
    description: "Starting location of waiter (format: x,y)",
    parseArgument: result =>
    {
        var waiters = new List<Waiter>();

        for (int i = 0; i < result.Tokens.Count; i++)
        {
            var token = result.Tokens[i];

            var parts = token.Value.Split(',');
            if (parts.Length != 2)
            {
                result.ErrorMessage =
                    $"Invalid format '{token.Value}'. Expected 'x,y'.";
                continue;
            }

            if (!int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                result.ErrorMessage = $"Invalid coordinates in '{token.Value}'.";
                continue;
            }

            waiters.Add(new Waiter(i, $"Waiter {i}", new Point(x, y)));
        }

        return waiters;
    })
{
    Arity = ArgumentArity.OneOrMore
};
var entranceOption = new Option<Point>(
    name: "--entrance",
    description: "Entrance location of the restaurant (format: x,y)",
    parseArgument: result =>
    {
        var token = result.Tokens.Single();

        var parts = token.Value.Split(',');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y))
        {
            result.ErrorMessage =
                $"Invalid entrance location '{token.Value}'. Expected format 'x,y'.";
            return new Point();
        }

        return new Point(x, y);
    })
{
    Arity = ArgumentArity.ExactlyOne
};
var kitchenOption = new Option<Point>(
    name: "--kitchen",
    description: "Entrance location of the kitchen (format: x,y)",
    parseArgument: result =>
    {
        var token = result.Tokens.Single();

        var parts = token.Value.Split(',');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y))
        {
            result.ErrorMessage =
                $"Invalid entrance location '{token.Value}'. Expected format 'x,y'.";
            return new Point();
        }

        return new Point(x, y);
    })
{
    Arity = ArgumentArity.ExactlyOne
};
var customerArrivalMinOption = new Option<double>("--arrival-mins", () => 5, "Mean minutes between customer arrivals");
var stopProbabilityOption = new Option<double>(
    "--stop-probability",
    () => 0.5,
    "Probability that a customer group stops growing at each additional person (0.0–1.0)");

stopProbabilityOption.AddValidator(result =>
{
    var value = result.GetValueOrDefault<double>();
    if (value <= 0.0 || value >= 1.0)
    {
        result.ErrorMessage = "Stop probability must be between 0 and 1 (exclusive).";
    }
});


simpleRestaurantCommand.AddOption(tablesOption);
simpleRestaurantCommand.AddOption(waitersOption);
simpleRestaurantCommand.AddOption(entranceOption);
simpleRestaurantCommand.AddOption(kitchenOption);
simpleRestaurantCommand.AddOption(customerArrivalMinOption);
simpleRestaurantCommand.AddOption(stopProbabilityOption);

simpleRestaurantCommand.SetHandler(
    (List<Table> tables, List<Waiter> waiters, Point entranceLocation, Point kitchenLocation,
    double customerArrivalMin, double stopProbability) =>
{
    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Seq("http://localhost:5341")
            .CreateLogger();

    // Create a logger factory that uses Serilog
    loggerFactory = new LoggerFactory().AddSerilog(Log.Logger);

    Func<Random, TimeSpan> customerInterArrivalTime = rnd => TimeSpan.FromMinutes(-customerArrivalMin * Math.Log(1.0 - rnd.NextDouble()));
    Func<Random, CustomerGroup> customerFactory = rnd => new CustomerGroup(SimpleRestaurant.SampleGeometricCustomerGroupSize(rnd, stopProbability), 0);

    Console.WriteLine("====== Running SimpleRestaurant ======");
    SimpleRestaurant.RunDemo(loggerFactory,
        tables, waiters, entranceLocation, kitchenLocation, customerInterArrivalTime, customerFactory);
}, tablesOption, waitersOption, entranceOption, kitchenOption,
    customerArrivalMinOption, stopProbabilityOption);

// ---- Demo: aws-rds-burst ----
var awsRdsBurstCommand = new Command("aws-rds-burst", "Run the AWS RDS Burst demo");

var familyOption = new Option<string>(
    name: "--family",
    description: "The RDS instance family (t3, t4g, m5).",
    getDefaultValue: () => "t3"
);

var sizeOption = new Option<string>(
    name: "--size",
    description: "The RDS instance size (micro, small, medium, large, xlarge).",
    getDefaultValue: () => "medium"
);

var awsRdsBurstDurationOption = new Option<double>(
    name: "--duration",
    description: "Total run duration in seconds.",
    getDefaultValue: () => 400.0
);

var initialCreditsOption = new Option<double>(
    name: "--initial-credits",
    description: "Initial CPU credits for the burstable instance.",
    getDefaultValue: () => 10.0
);

var unlimitedCreditsOption = new Option<bool>(
    name: "--unlimited-credits",
    description: "Whether the burstable instance has unlimited CPU credits.",
    getDefaultValue: () => false
);

var grafanaOption = new Option<bool>(
    name: "--grafana",
    description: "Enable OpenTelemetry export to Grafana Cloud (requires API key configuration).",
    getDefaultValue: () => false
);

awsRdsBurstCommand.AddOption(familyOption);
awsRdsBurstCommand.AddOption(sizeOption);
awsRdsBurstCommand.AddOption(awsRdsBurstDurationOption);
awsRdsBurstCommand.AddOption(initialCreditsOption);
awsRdsBurstCommand.AddOption(unlimitedCreditsOption);
awsRdsBurstCommand.AddOption(grafanaOption);

awsRdsBurstCommand.SetHandler((string family, string size, double duration, double initialCredits, bool isUnlimitedCredits, bool enableGrafana) =>
{
    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

    // Create a logger factory that uses Serilog
    loggerFactory = new LoggerFactory().AddSerilog(Log.Logger);
    
    var spec = AwsRdsRegistry.GetSpec(family, size);
    var rdsBehavior = new AwsRdsBehavior(spec, initialCredits, isUnlimitedCredits);

    Console.WriteLine($"====== Running AWS RDS Burst Demo (Instance={family}.{size}, Duration={duration} seconds) ======");
    Console.WriteLine($"Initial Credits: {initialCredits}, Unlimited Mode: {isUnlimitedCredits}");
    AwsBurstScenario.RunDemo(
        loggerFactory,
        duration,
        rdsBehavior,
        genSeed: 1234,
        enableGrafana: enableGrafana
    );
}, familyOption, sizeOption, awsRdsBurstDurationOption, initialCreditsOption, unlimitedCreditsOption, grafanaOption);

// ---- Demo: azure-db-burst ----
var azureDbBurstCommand = new Command("azure-db-burst", "Run the Azure Database Burst demo");

var seriesOption = new Option<string>(
    name: "--series",
    description: "The Azure instance series. Currently supported: B (Burstable).",
    getDefaultValue: () => "B"
);

var azureSizeOption = new Option<string>(
    name: "--size",
    description: "The Azure instance size. B-series: 1ms, 2s, 2ms, 4ms, 8ms.",
    getDefaultValue: () => "2ms"
);

var azureDbBurstDurationOption = new Option<double>(
    name: "--duration",
    description: "Total run duration in seconds.",
    getDefaultValue: () => 400.0
);

var azureInitialCreditsOption = new Option<double>(
    name: "--initial-credits",
    description: "Initial CPU credits for the burstable instance.",
    getDefaultValue: () => 60.0
);

var azureGrafanaOption = new Option<bool>(
    name: "--grafana",
    description: "Enable OpenTelemetry export to Grafana Cloud (requires API key configuration).",
    getDefaultValue: () => false
);

azureDbBurstCommand.AddOption(seriesOption);
azureDbBurstCommand.AddOption(azureSizeOption);
azureDbBurstCommand.AddOption(azureDbBurstDurationOption);
azureDbBurstCommand.AddOption(azureInitialCreditsOption);
azureDbBurstCommand.AddOption(azureGrafanaOption);

azureDbBurstCommand.SetHandler((string series, string size, double duration, double initialCredits, bool enableGrafana) =>
{
    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

    // Create a logger factory that uses Serilog
    loggerFactory = new LoggerFactory().AddSerilog(Log.Logger);

    AzureDbInstanceSpec spec;
    try
    {
        spec = AzureDbRegistry.GetSpec(series, size);
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("Currently supported Azure Database instances:");
        Console.WriteLine("  B-series (Burstable): B.1ms, B.2s, B.2ms, B.4ms, B.8ms");
        Console.WriteLine();
        Console.WriteLine("D-series (General Purpose) and E-series (Memory Optimized) coming soon.");
        return;
    }

    var dbBehavior = new AzureDbBehavior(spec, initialCredits);

    Console.WriteLine($"====== Running Azure Database Burst Demo (Instance={series}.{size}, Duration={duration} seconds) ======");
    Console.WriteLine($"Initial Credits: {initialCredits}");
    AzureDbBurstScenario.RunDemo(
        loggerFactory,
        duration,
        dbBehavior,
        genSeed: 1234,
        enableGrafana: enableGrafana
    );
}, seriesOption, azureSizeOption, azureDbBurstDurationOption, azureInitialCreditsOption, azureGrafanaOption);

// ---- Demo: azure-pgsql-pooling ----
var azurePgsqlPoolingCommand = new Command("azure-pgsql-pooling", "Compare PostgreSQL connection pooling strategies on Azure B-series");

var poolModeOption = new Option<string>(
    name: "--mode",
    description: "Pooling mode: direct, session, transaction.",
    getDefaultValue: () => "direct"
);

var poolSizeOption = new Option<int>(
    name: "--pool-size",
    description: "Connection pool size (ignored for direct mode).",
    getDefaultValue: () => 20
);

var poolingSeriesOption = new Option<string>(
    name: "--series",
    description: "The Azure instance series. Currently supported: B (Burstable).",
    getDefaultValue: () => "B"
);

var poolingSizeOption = new Option<string>(
    name: "--size",
    description: "The Azure instance size. B-series: 1ms, 2s, 2ms, 4ms, 8ms.",
    getDefaultValue: () => "2ms"
);

var poolingDurationOption = new Option<double>(
    name: "--duration",
    description: "Total run duration in seconds.",
    getDefaultValue: () => 300.0
);

var poolingInitialCreditsOption = new Option<double>(
    name: "--initial-credits",
    description: "Initial CPU credits for the burstable instance.",
    getDefaultValue: () => 60.0
);

var poolingGrafanaOption = new Option<bool>(
    name: "--grafana",
    description: "Enable OpenTelemetry export to Grafana Cloud (requires API key configuration).",
    getDefaultValue: () => false
);

azurePgsqlPoolingCommand.AddOption(poolModeOption);
azurePgsqlPoolingCommand.AddOption(poolSizeOption);
azurePgsqlPoolingCommand.AddOption(poolingSeriesOption);
azurePgsqlPoolingCommand.AddOption(poolingSizeOption);
azurePgsqlPoolingCommand.AddOption(poolingDurationOption);
azurePgsqlPoolingCommand.AddOption(poolingInitialCreditsOption);
azurePgsqlPoolingCommand.AddOption(poolingGrafanaOption);

azurePgsqlPoolingCommand.SetHandler((string mode, int poolSize, string series, string size, double duration, double initialCredits, bool enableGrafana) =>
{
    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

    // Create a logger factory that uses Serilog
    loggerFactory = new LoggerFactory().AddSerilog(Log.Logger);

    // Parse pooling mode with user-friendly names
    PoolingMode poolMode;
    try
    {
        poolMode = mode.ToLowerInvariant() switch
        {
            "direct" => PoolingMode.Direct,
            "session" => PoolingMode.SessionPooling,
            "transaction" => PoolingMode.TransactionPooling,
            _ => throw new ArgumentException($"Invalid pooling mode '{mode}'. Valid options: direct, session, transaction")
        };

        // Validate pool size for pooling modes
        if (poolMode != PoolingMode.Direct && poolSize <= 0)
        {
            throw new ArgumentException($"Pool size must be positive for {poolMode} mode (got {poolSize}). Use --mode direct if you don't want pooling.");
        }
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return;
    }

    // Get Azure DB spec
    AzureDbInstanceSpec spec;
    try
    {
        spec = AzureDbRegistry.GetSpec(series, size);
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("Currently supported Azure Database instances:");
        Console.WriteLine("  B-series (Burstable): B.1ms, B.2s, B.2ms, B.4ms, B.8ms");
        return;
    }

    var dbBehavior = new AzureDbBehavior(spec, initialCredits);

    Console.WriteLine($"====== Running Azure PostgreSQL Pooling Demo ======");
    Console.WriteLine($"Instance: {series}.{size}");
    Console.WriteLine($"Pooling Mode: {poolMode}");
    Console.WriteLine($"Pool Size: {(poolMode == PoolingMode.Direct ? "N/A (Direct)" : poolSize.ToString())}");
    Console.WriteLine($"Initial Credits: {initialCredits}");
    Console.WriteLine($"Duration: {duration} seconds");

    AzurePgsqlPoolingScenario.RunDemo(
        loggerFactory,
        duration,
        dbBehavior,
        poolMode,
        poolSize,
        genSeed: 1234,
        enableGrafana: enableGrafana
    );
}, poolModeOption, poolSizeOption, poolingSeriesOption, poolingSizeOption, poolingDurationOption, poolingInitialCreditsOption, poolingGrafanaOption);

// ---- Group commands ----
var demoCommand = new Command("demo", "Run a simulation demo");
demoCommand.AddCommand(simpleGenCommand);
demoCommand.AddCommand(simpleServerCommand);
demoCommand.AddCommand(mmckCommand);
demoCommand.AddCommand(simpleRestaurantCommand);
demoCommand.AddCommand(awsRdsBurstCommand);
demoCommand.AddCommand(azureDbBurstCommand);
demoCommand.AddCommand(azurePgsqlPoolingCommand);

rootCommand.AddCommand(demoCommand);

return await rootCommand.InvokeAsync(args);