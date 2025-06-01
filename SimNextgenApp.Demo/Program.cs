using Microsoft.Extensions.Logging;
using SimNextgenApp.Demo.Scenarios;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("SimNextgenApp", LogLevel.Trace)
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddConsole();
});

Console.WriteLine("====== Running SimpleGenerator ======");
SimpleGenerator.RunDemo(loggerFactory);

Console.WriteLine("====== Running SimpleServerAndGenerator ======");
SimpleServerAndGenerator.RunDemo(loggerFactory);

Console.WriteLine("====== Running SimpleMmck ======");
SimpleMmck.RunDemo(loggerFactory);