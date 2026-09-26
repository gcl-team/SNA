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
    rootCommand.Parse("-h").Invoke(); // Show help
    return 1;
}

// ---- Demo: simple-generator ----
var simpleGenCommand = new Command("simple-generator", "Run the SimpleGenerator demo");
simpleGenCommand.SetAction(_ =>
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
var meanArrivalSecondsOption = new Option<double>("--arrival-secs")
{
    Description = "Mean arrival time in seconds.",
    DefaultValueFactory = _ => 5.0
};

var simpleServerCommand = new Command("simple-server", "Run the SimpleServerAndGenerator demo");
simpleServerCommand.Options.Add(meanArrivalSecondsOption);

simpleServerCommand.SetAction(parseResult =>
{
    var meanArrivalSeconds = parseResult.GetValue(meanArrivalSecondsOption);

    Console.WriteLine($"====== Running SimpleServerAndGenerator (Mean Arrival (Unit: second)={meanArrivalSeconds}) ======");
    SimpleServerAndGenerator.RunDemo(loggerFactory, meanArrivalSeconds);
});

// ---- Demo: M/M/c/K ----
var mmckCommand = new Command("mmck", "Run the SimpleMmck demo");

// Define options
var serversOption = new Option<int>("--servers") { Description = "Number of servers", DefaultValueFactory = _ => 2 };
var capacityOption = new Option<int>("--capacity") { Description = "System capacity (K)", DefaultValueFactory = _ => 5 };
var arrivalSecsOption = new Option<double>("--arrival-secs") { Description = "Mean seconds between arrivals", DefaultValueFactory = _ => 3.0 };
var serviceSecsOption = new Option<double>("--service-secs") { Description = "Mean service time per server (seconds)", DefaultValueFactory = _ => 5.0 };
var durationOption = new Option<double>("--duration") { Description = "Total simulation time", DefaultValueFactory = _ => 500.0 };
var warmupOption = new Option<double>("--warmup") { Description = "Warmup time before collecting stats", DefaultValueFactory = _ => 100.0 };
var genSeedOption = new Option<int>("--gen-seed") { Description = "Random seed for generator", DefaultValueFactory = _ => 2024 };
var serverSeedBaseOption = new Option<int>("--server-seed-base") { Description = "Seed base for all servers", DefaultValueFactory = _ => 100 };

// Add options one-by-one
mmckCommand.Options.Add(serversOption);
mmckCommand.Options.Add(capacityOption);
mmckCommand.Options.Add(arrivalSecsOption);
mmckCommand.Options.Add(serviceSecsOption);
mmckCommand.Options.Add(durationOption);
mmckCommand.Options.Add(warmupOption);
mmckCommand.Options.Add(genSeedOption);
mmckCommand.Options.Add(serverSeedBaseOption);

// Set handler
mmckCommand.SetAction(parseResult =>
{
    var servers = parseResult.GetValue(serversOption);
    var capacity = parseResult.GetValue(capacityOption);
    var arrivalSecs = parseResult.GetValue(arrivalSecsOption);
    var serviceSecs = parseResult.GetValue(serviceSecsOption);
    var duration = parseResult.GetValue(durationOption);
    var warmup = parseResult.GetValue(warmupOption);
    var genSeed = parseResult.GetValue(genSeedOption);
    var serverSeedBase = parseResult.GetValue(serverSeedBaseOption);

    Console.WriteLine($"====== Running MMCK Demo (c={servers}, K={capacity}) ======");
    SimpleMmck.RunDemo(
        loggerFactory,
        servers, capacity, arrivalSecs, serviceSecs,
        duration, warmup, genSeed, serverSeedBase
    );
});

// ---- Demo: simple-restaurant ----
var simpleRestaurantCommand = new Command("simple-restaurant", "Run the SimpleRestaurant demo");

// Define options
var tablesOption = new Option<List<Table>>("--table")
{
    Description = "Capacity and location of table (format: capacity,x,y)",
    CustomParser = result =>
    {
        var tables = new List<Table>();

        for (int i = 0; i < result.Tokens.Count; i++)
        {
            var token = result.Tokens[i];

            var parts = token.Value.Split(',');
            if (parts.Length != 3)
            {
                result.AddError($"Invalid format '{token.Value}'. Expected 'capacity,x,y'.");
                continue;
            }

            if (!int.TryParse(parts[0], out var capacity))
            {
                result.AddError($"Invalid capacity in '{token.Value}'.");
                continue;
            }

            if (!int.TryParse(parts[1], out var x) || !int.TryParse(parts[2], out var y))
            {
                result.AddError($"Invalid coordinates in '{token.Value}'.");
                continue;
            }

            tables.Add(new Table(i, capacity, new Point(x, y)));
        }

        return tables;
    },
    Arity = ArgumentArity.OneOrMore,
    Required = true
};
var waitersOption = new Option<List<Waiter>>("--waiter")
{
    Description = "Starting location of waiter (format: x,y)",
    CustomParser = result =>
    {
        var waiters = new List<Waiter>();

        for (int i = 0; i < result.Tokens.Count; i++)
        {
            var token = result.Tokens[i];

            var parts = token.Value.Split(',');
            if (parts.Length != 2)
            {
                result.AddError($"Invalid format '{token.Value}'. Expected 'x,y'.");
                continue;
            }

            if (!int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
            {
                result.AddError($"Invalid coordinates in '{token.Value}'.");
                continue;
            }

            waiters.Add(new Waiter(i, $"Waiter {i}", new Point(x, y)));
        }

        return waiters;
    },
    Arity = ArgumentArity.OneOrMore,
    Required = true
};
var entranceOption = new Option<Point>("--entrance")
{
    Description = "Entrance location of the restaurant (format: x,y)",
    CustomParser = result =>
    {
        var token = result.Tokens.Single();

        var parts = token.Value.Split(',');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y))
        {
            result.AddError($"Invalid entrance location '{token.Value}'. Expected format 'x,y'.");
            return new Point();
        }

        return new Point(x, y);
    },
    Arity = ArgumentArity.ExactlyOne,
    Required = true
};
var kitchenOption = new Option<Point>("--kitchen")
{
    Description = "Entrance location of the kitchen (format: x,y)",
    CustomParser = result =>
    {
        var token = result.Tokens.Single();

        var parts = token.Value.Split(',');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y))
        {
            result.AddError($"Invalid entrance location '{token.Value}'. Expected format 'x,y'.");
            return new Point();
        }

        return new Point(x, y);
    },
    Arity = ArgumentArity.ExactlyOne,
    Required = true
};
var customerArrivalMinOption = new Option<double>("--arrival-mins") { Description = "Mean minutes between customer arrivals", DefaultValueFactory = _ => 5 };
var stopProbabilityOption = new Option<double>("--stop-probability") { Description = "Probability that a customer group stops growing at each additional person (0.0–1.0)", DefaultValueFactory = _ => 0.5 };

stopProbabilityOption.Validators.Add(result =>
{
    var value = result.GetValueOrDefault<double>();
    if (value <= 0.0 || value >= 1.0)
    {
        result.AddError("Stop probability must be between 0 and 1 (exclusive).");
    }
});


simpleRestaurantCommand.Options.Add(tablesOption);
simpleRestaurantCommand.Options.Add(waitersOption);
simpleRestaurantCommand.Options.Add(entranceOption);
simpleRestaurantCommand.Options.Add(kitchenOption);
simpleRestaurantCommand.Options.Add(customerArrivalMinOption);
simpleRestaurantCommand.Options.Add(stopProbabilityOption);

simpleRestaurantCommand.SetAction(parseResult =>
{
    var tables = parseResult.GetRequiredValue(tablesOption);
    var waiters = parseResult.GetRequiredValue(waitersOption);
    var entranceLocation = parseResult.GetRequiredValue(entranceOption);
    var kitchenLocation = parseResult.GetRequiredValue(kitchenOption);
    var customerArrivalMin = parseResult.GetValue(customerArrivalMinOption);
    var stopProbability = parseResult.GetValue(stopProbabilityOption);

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
});

// ---- Demo: aws-rds-burst ----
var awsRdsBurstCommand = new Command("aws-rds-burst", "Run the AWS RDS Burst demo");

var familyOption = new Option<string>("--family")
{
    Description = "The RDS instance family (t3, t4g, m5).",
    DefaultValueFactory = _ => "t3"
};

var sizeOption = new Option<string>("--size")
{
    Description = "The RDS instance size (micro, small, medium, large, xlarge).",
    DefaultValueFactory = _ => "medium"
};

var awsRdsBurstDurationOption = new Option<double>("--duration")
{
    Description = "Total run duration in seconds.",
    DefaultValueFactory = _ => 400.0
};

var initialCreditsOption = new Option<double>("--initial-credits")
{
    Description = "Initial CPU credits for the burstable instance.",
    DefaultValueFactory = _ => 10.0
};

var unlimitedCreditsOption = new Option<bool>("--unlimited-credits")
{
    Description = "Whether the burstable instance has unlimited CPU credits.",
    DefaultValueFactory = _ => false
};

var grafanaOption = new Option<bool>("--grafana")
{
    Description = "Enable OpenTelemetry export to Grafana Cloud (requires API key configuration).",
    DefaultValueFactory = _ => false
};

awsRdsBurstCommand.Options.Add(familyOption);
awsRdsBurstCommand.Options.Add(sizeOption);
awsRdsBurstCommand.Options.Add(awsRdsBurstDurationOption);
awsRdsBurstCommand.Options.Add(initialCreditsOption);
awsRdsBurstCommand.Options.Add(unlimitedCreditsOption);
awsRdsBurstCommand.Options.Add(grafanaOption);

awsRdsBurstCommand.SetAction(parseResult =>
{
    var family = parseResult.GetRequiredValue(familyOption);
    var size = parseResult.GetRequiredValue(sizeOption);
    var duration = parseResult.GetValue(awsRdsBurstDurationOption);
    var initialCredits = parseResult.GetValue(initialCreditsOption);
    var isUnlimitedCredits = parseResult.GetValue(unlimitedCreditsOption);
    var enableGrafana = parseResult.GetValue(grafanaOption);

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
});

// ---- Demo: azure-db-burst ----
var azureDbBurstCommand = new Command("azure-db-burst", "Run the Azure Database Burst demo");

var seriesOption = new Option<string>("--series")
{
    Description = "The Azure instance series. Currently supported: B (Burstable).",
    DefaultValueFactory = _ => "B"
};

var azureSizeOption = new Option<string>("--size")
{
    Description = "The Azure instance size. B-series: 1ms, 2s, 2ms, 4ms, 8ms.",
    DefaultValueFactory = _ => "2ms"
};

var azureDbBurstDurationOption = new Option<double>("--duration")
{
    Description = "Total run duration in seconds.",
    DefaultValueFactory = _ => 400.0
};

var azureInitialCreditsOption = new Option<double>("--initial-credits")
{
    Description = "Initial CPU credits for the burstable instance.",
    DefaultValueFactory = _ => 60.0
};

var azureGrafanaOption = new Option<bool>("--grafana")
{
    Description = "Enable OpenTelemetry export to Grafana Cloud (requires API key configuration).",
    DefaultValueFactory = _ => false
};

azureDbBurstCommand.Options.Add(seriesOption);
azureDbBurstCommand.Options.Add(azureSizeOption);
azureDbBurstCommand.Options.Add(azureDbBurstDurationOption);
azureDbBurstCommand.Options.Add(azureInitialCreditsOption);
azureDbBurstCommand.Options.Add(azureGrafanaOption);

azureDbBurstCommand.SetAction(parseResult =>
{
    var series = parseResult.GetRequiredValue(seriesOption);
    var size = parseResult.GetRequiredValue(azureSizeOption);
    var duration = parseResult.GetValue(azureDbBurstDurationOption);
    var initialCredits = parseResult.GetValue(azureInitialCreditsOption);
    var enableGrafana = parseResult.GetValue(azureGrafanaOption);

    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            //.WriteTo.Console()
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
});

// ---- Demo: azure-pgsql-pooling ----
var azurePgsqlPoolingCommand = new Command("azure-pgsql-pooling", "Compare PostgreSQL connection pooling strategies on Azure B-series");

var poolModeOption = new Option<string>("--mode")
{
    Description = "Pooling mode: direct, session, transaction.",
    DefaultValueFactory = _ => "direct"
};

var poolSizeOption = new Option<int>("--pool-size")
{
    Description = "Connection pool size (ignored for direct mode).",
    DefaultValueFactory = _ => 20
};

var poolingSeriesOption = new Option<string>("--series")
{
    Description = "The Azure instance series. Currently supported: B (Burstable).",
    DefaultValueFactory = _ => "B"
};

var poolingSizeOption = new Option<string>("--size")
{
    Description = "The Azure instance size. B-series: 1ms, 2s, 2ms, 4ms, 8ms.",
    DefaultValueFactory = _ => "2ms"
};

var poolingDurationOption = new Option<double>("--duration")
{
    Description = "Total run duration in seconds.",
    DefaultValueFactory = _ => 300.0
};

var poolingInitialCreditsOption = new Option<double>("--initial-credits")
{
    Description = "Initial CPU credits for the burstable instance.",
    DefaultValueFactory = _ => 60.0
};

var poolingGrafanaOption = new Option<bool>("--grafana")
{
    Description = "Enable OpenTelemetry export to Grafana Cloud (requires API key configuration).",
    DefaultValueFactory = _ => false
};

azurePgsqlPoolingCommand.Options.Add(poolModeOption);
azurePgsqlPoolingCommand.Options.Add(poolSizeOption);
azurePgsqlPoolingCommand.Options.Add(poolingSeriesOption);
azurePgsqlPoolingCommand.Options.Add(poolingSizeOption);
azurePgsqlPoolingCommand.Options.Add(poolingDurationOption);
azurePgsqlPoolingCommand.Options.Add(poolingInitialCreditsOption);
azurePgsqlPoolingCommand.Options.Add(poolingGrafanaOption);

azurePgsqlPoolingCommand.SetAction(parseResult =>
{
    var mode = parseResult.GetRequiredValue(poolModeOption);
    var poolSize = parseResult.GetValue(poolSizeOption);
    var series = parseResult.GetRequiredValue(poolingSeriesOption);
    var size = parseResult.GetRequiredValue(poolingSizeOption);
    var duration = parseResult.GetValue(poolingDurationOption);
    var initialCredits = parseResult.GetValue(poolingInitialCreditsOption);
    var enableGrafana = parseResult.GetValue(poolingGrafanaOption);

    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            //.WriteTo.Console()
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
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return;
    }

    // Validate pool size only when pooling is enabled (not in direct mode)
    if (poolMode != PoolingMode.Direct && poolSize <= 0)
    {
        Console.WriteLine("Error: Pool size must be positive when using session or transaction pooling.");
        Console.WriteLine($"Got: --pool-size {poolSize}");
        Console.WriteLine("Hint: Pool size is only ignored for --mode direct.");
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
});

// ---- Group commands ----
var demoCommand = new Command("demo", "Run a simulation demo");
demoCommand.Subcommands.Add(simpleGenCommand);
demoCommand.Subcommands.Add(simpleServerCommand);
demoCommand.Subcommands.Add(mmckCommand);
demoCommand.Subcommands.Add(simpleRestaurantCommand);
demoCommand.Subcommands.Add(awsRdsBurstCommand);
demoCommand.Subcommands.Add(azureDbBurstCommand);
demoCommand.Subcommands.Add(azurePgsqlPoolingCommand);

rootCommand.Subcommands.Add(demoCommand);

return await rootCommand.Parse(args).InvokeAsync();