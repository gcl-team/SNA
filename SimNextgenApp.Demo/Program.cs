using Microsoft.Extensions.Logging;
using Serilog;
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
    (List<Table> tables, List<Waiter> waiters, Point entraceLocation, Point kitchenLocation,
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
        tables, waiters, entraceLocation, kitchenLocation, customerInterArrivalTime, customerFactory);
}, tablesOption, waitersOption, entranceOption, kitchenOption,
    customerArrivalMinOption, stopProbabilityOption);

// ---- Group commands ----
var demoCommand = new Command("demo", "Run a simulation demo");
demoCommand.AddCommand(simpleGenCommand);
demoCommand.AddCommand(simpleServerCommand);
demoCommand.AddCommand(mmckCommand);
demoCommand.AddCommand(simpleRestaurantCommand);

rootCommand.AddCommand(demoCommand);

return await rootCommand.InvokeAsync(args);