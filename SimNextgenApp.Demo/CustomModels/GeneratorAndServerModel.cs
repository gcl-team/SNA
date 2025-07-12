using Microsoft.Extensions.Logging;
using SimNextgenApp.Configurations;
using SimNextgenApp.Core;
using SimNextgenApp.Modeling;
using SimNextgenApp.Modeling.Generator;
using SimNextgenApp.Modeling.Server;
using SimNextgenApp.Statistics;

namespace SimNextgenApp.Demo.CustomModels;

/// <summary>
/// This is a textbook example of a simple M/M/1/N queue
/// In this case, it is M/M/1/1 + balking because N=1, server capacity is 1, 
/// and there's no waiting room, so entities balk if the server is busy.
/// </summary>
internal class GeneratorAndServerModel : AbstractSimulationModel
{
    public Generator<MyLoad> LoadGenerator { get; }
    public Server<MyLoad> ServicePoint { get; }
    public ServerObserver<MyLoad> ServicePointObserver { get; }
    public int BalkedLoadsCount { get; private set; } = 0;

    private readonly ILogger<GeneratorAndServerModel> _modelLogger;
    private IRunContext _runContext = null!;

    public GeneratorAndServerModel(
        GeneratorStaticConfig<MyLoad> genConfig, int genSeed,
        ServerStaticConfig<MyLoad> serverConfig, int serverSeed,
        ILoggerFactory loggerFactory, string name = "SimpleSystem")
        : base(name)
    {
        _modelLogger = loggerFactory.CreateLogger<GeneratorAndServerModel>();

        LoadGenerator = new Generator<MyLoad>(genConfig, genSeed, "Arrivals", loggerFactory);
        ServicePoint = new Server<MyLoad>(serverConfig, serverSeed, "Processor"); // Server constructor doesn't take loggerFactory yet

        // Connect Generator to Server
        LoadGenerator.LoadGenerated += HandleLoadGenerated;

        // Log when server sends a load away
        ServicePoint.LoadDeparted += (load, departureTime) =>
        {
            load.ServiceEndTime = departureTime;
            _modelLogger.LogInformation($"--- [LOAD DEPARTED] SimTime: {departureTime:F2} -> {load}. Service time: {load.ServiceEndTime - load.ServiceStartTime:F2} ---");
        };

        ServicePointObserver = new ServerObserver<MyLoad>(this.ServicePoint);
    }

    private void HandleLoadGenerated(MyLoad load, double generationTime)
    {
        load.CreationTime = generationTime;
        _modelLogger.LogInformation($"--- [LOAD GENERATED] SimTime: {generationTime:F2} -> {load}. Attempting service...");

        bool accepted = ServicePoint.TryStartService(load, _runContext);
        if (accepted)
        {
            load.ServiceStartTime = generationTime; // Or actual service start time if ServerStartServiceEvent passes it back
            _modelLogger.LogInformation($"      Load {load.Id} accepted by Server '{ServicePoint.Name}'.");
        }
        else
        {
            BalkedLoadsCount++;
            _modelLogger.LogWarning($"      Load {load.Id} BALKED. Server '{ServicePoint.Name}' is full (InService: {ServicePoint.NumberInService}/{ServicePoint.Capacity}).");
        }
    }

    public override void Initialize(IRunContext engineContext)
    {
        ArgumentNullException.ThrowIfNull(engineContext);

        _runContext = engineContext;

        LoadGenerator.Initialize(engineContext); // Generator will schedule its GeneratorStartEvent
    }

    public override void WarmedUp(double simulationTime)
    {
        LoadGenerator.WarmedUp(simulationTime);
        ServicePoint.WarmedUp(simulationTime);
        ServicePointObserver.WarmedUp(simulationTime);
        BalkedLoadsCount = 0; // Reset balk count after warm-up
        _modelLogger.LogInformation("--- System Warmed Up at {WarmupTime}. Statistics reset. ---", simulationTime);
    }
}