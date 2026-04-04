# SNA

## 👋 Welcome to SNA!

SNA is a lightweight open-source library for building Discrete Event Simulations (DES) in C# and .NET. 

It is built from the ground up on the .NET technology. It leverages modern C# features and a clean, extensible architecture for modelling and simulation.

## 📐 Modeling Formalism

SNA uses the **event-oriented** DES formalism.

- Simulation progresses by executing **scheduled events in timestamp order**;
- Events are added to a **Future Event List (FEL)** and executed in a loop;
- Simulation time advances **only** when the next event is processed.

This makes SNA suitable for:
- Queuing systems (e.g., banks, networks, logistics);
- Transaction-based models (e.g., arrivals, service, routing);
- Simulations where events are sparse in time.

## 🚀 Getting Started (NuGet)

SNA is available on [GitHub Packages - NuGet](https://github.com/gcl-team/SNA/pkgs/nuget/SimNextGenApp). We can install it using the .NET CLI:

```bash
dotnet add package SimNextgenApp
```

This package is hosted on GitHub Packages, which requires authentication. In the root of your project, copy the example config file

```bash
cp nuget.config.example nuget.config
```

Then edit `nuget.config`. You'll need to put in your GitHub username and a [personal access token (PAT)](https://github.com/settings/tokens).

## 🛠️ Development and Local Testing

To use SNA in your own project, you can reference it directly from the source code. It is easy!

First, please clone the SNA source code onto your local machine.

Secondly, reference the `SimNextgenApp` project. There are two common ways to add the project reference to your solution.

**Method A: Using Visual Studio**

1. Open your own solution (.sln) in Visual Studio.
2. In the Solution Explorer, right-click on your Solution and choose Add -> Existing Project....
3. Navigate to the cloned `SimNextgenApp` folder and select `SimNextgenApp.csproj`.
4. Now, right-click on your project Dependencies (or References) node and choose Add Project Reference....
5. Check the box next to SimNextgenApp and click OK.

**Method B: Using the .NET CLI**

1. Navigate to your project directory in the terminal.
2. Add a reference to the SimNextgenApp project file using its path.
   ```bash
   dotnet add reference ../path/to/cloned/repo/SimNextgenApp/SimNextgenApp.csproj
   ```

That's it! You can now using SNA and start building your simulation.

### Demo

For a more detailed example, check out our [SimNextgenApp.Demo project](https://github.com/gcl-team/SNA/tree/main/SimNextgenApp.Demo)!

## 📊 Observability

SNA includes built-in OpenTelemetry-based observability supporting all three pillars: **Traces**, **Metrics**, and **Logs**.

### Quick Start

```csharp
// Configure telemetry with console exporter (for development)
var telemetry = SimulationTelemetry.Create()
    .WithConsoleExporter()
    .WithLogging(includeConsoleExporter: true)
    .Build();

// Observe component metrics
var serverObserver = ServerObserver.CreateSimple(server);
var queueObserver = QueueObserver.CreateSimple(queue);
var generatorObserver = GeneratorObserver.CreateSimple(generator);
var resourceObserver = ResourceObserver.CreateSimple(resourcePool);

// Observe whole-simulation metrics
var simulationObserver = SimulationObserver.CreateSimple(engine);

// IMPORTANT: Set time unit for proper time conversion
// This should be called in your model's Initialize() method
// Required for observers that record time-based histograms
serverObserver.SetTimeUnit(context.TimeUnit);      // For sojourn time
queueObserver.SetTimeUnit(context.TimeUnit);       // For wait time
generatorObserver.SetTimeUnit(context.TimeUnit);   // For inter-arrival time
// Note: ResourceObserver and SimulationObserver don't require SetTimeUnit

// Access metrics
Console.WriteLine($"Server Utilization: {serverObserver.Utilization:F2}");
Console.WriteLine($"Queue Occupancy: {queueObserver.Occupancy}");
Console.WriteLine($"Total Events: {simulationObserver.TotalEventsExecuted}");
```

### Exporters

SNA supports multiple exporters for different environments:

#### Console Exporter (Development Only)

```csharp
var telemetry = SimulationTelemetry.Create()
    .WithConsoleExporter()
    .WithLogging(includeConsoleExporter: true)
    .Build();
```

**Use for**: Local development, debugging, learning, demos

**⚠️ Not for Production**: The console exporter is intended for debugging and learning purposes only. The output format is not standardized and can change at any time. It lacks the reliability, performance, and features needed for production observability.

**Why we keep it**: Essential for development workflow:
- Instant feedback during development without external dependencies
- Perfect for demos and tutorials
- Useful for unit tests and debugging
- Zero infrastructure setup required

**Production recommendation**: Use `.WithOtlpExporter()` with a production observability backend.

#### Production Exporters

**OTLP (OpenTelemetry Protocol)** - For production observability backends:

```csharp
// Generic OTLP endpoint
var telemetry = SimulationTelemetry.Create()
    .WithOtlpExporter("http://localhost:4317")
    .WithLogging(includeOtlpExporter: true)
    .Build();

// Backend-specific presets (Grafana Cloud, Datadog, Honeycomb)
var telemetry = SimulationTelemetry.Create()
    .WithOtlpExporter(OtlpBackend.GrafanaCloud, apiKey: "instanceId:apiToken", region: "us-central-0")
    .WithLogging(includeOtlpExporter: true)
    .Build();
```

**Use for**: Production deployments, cloud observability platforms

**Prometheus HttpListener** - For direct Prometheus scraping:

```csharp
var telemetry = SimulationTelemetry.Create()
    .WithPrometheusExporter(port: 9090)
    .Build();
// Metrics available at: http://localhost:9090/metrics
```

**Use for**: Local development with Prometheus, simple deployments, learning

**⚠️ Production Note**: While Prometheus itself is production-ready, OpenTelemetry's HttpListener exporter is not their recommended production approach. For production environments, prefer **OTLP → OpenTelemetry Collector → Prometheus Remote Write** instead of direct scraping.

**Why we keep it**: Despite the warning, this exporter is useful for:
- Simple local Prometheus setups without OTLP Collector infrastructure
- Development and testing environments
- Learning and prototyping
- Small-scale deployments where OTLP infrastructure isn't justified

**Production recommendation**: Use `.WithOtlpExporter()` and configure your observability backend (Grafana Cloud, Datadog, etc.) to ingest OTLP data. This is the OpenTelemetry team's recommended production path and supports all three pillars (traces, metrics, logs).

### Available Observers

- **ServerObserver** - Tracks server utilization, loads completed, sojourn time *(requires SetTimeUnit)*
- **QueueObserver** - Monitors queue occupancy, wait time, throughput (enqueued/dequeued/balked) *(requires SetTimeUnit)*
- **GeneratorObserver** - Observes load generation rate, inter-arrival time *(requires SetTimeUnit)*
- **ResourceObserver** - Tracks resource utilization, availability, waiting count
- **SimulationObserver** - Whole-simulation metrics (events executed, clock time, performance)

**Note:** Observers that record time-based histograms (Server, Queue, Generator) require `SetTimeUnit()` to be called during model initialization. This converts simulation time units to seconds for proper OpenTelemetry metric reporting. If not set, an `InvalidOperationException` will be thrown when time-based metrics are recorded.

For complete examples, see [SimpleMmck.cs](SimNextgenApp.Demo/Scenarios/SimpleMmck.cs).

## 🧠 Core Concepts

Understanding these concepts will help you master SimNextgen.

- SimulationEngine ⚙️: 
	- The main manager of the simulation;
	- Keeps track of the simulation time, FEL, and runs the simulation step-by-step;
	- This is where the main simulation logic happens.
- IScheduler ✉️: 
	- Interface for scheduling future events;
	- Provided by the engine to simulation models.
- IRunContext 🔎: 
	- A way to look at the current status of the simulation (like the current time and how many events have happened);
	- The `SimulationEngine` shows this to the `IRunStrategy`, which uses this info to decide when to stop the simulation.
- IRunStrategy 🏁: 
	- Defines stopping conditions (e.g., run for 100 minutes or until 10,000 events).
- ISimulationModel & Components 🧱: 
	- Blueprint for building the simulation;
	- Initialises and wires up components and events.
- AbstractEvent ⚡: 
	- The basic building block for anything that happens in the simulation;
	- Events are straightforward instructions: they each have an `Execute` method that says what to do;
	- When it's time for an event, the `SimulationEngine` moves the simulation clock to the event's time, runs its `Execute` method. This usually changes something in the model components and often lines up new events for the future.

## 🤝 How to Contribute

We welcome your contributions! Bug reports and feature suggestions are encouraged. 
Open issues or submit pull requests via [Project Issues](https://github.com/gcl-team/SNA/issues).

## 📜 License
This library is distributed under the MIT License. See [LICENSE](https://github.com/gcl-team/SNA/blob/main/LICENSE) for more information.
