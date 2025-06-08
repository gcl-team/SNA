# SNA

## 👋 Welcome to SNA!

SNA is a lightweight open-source library for building Discrete Event Simulations (DES) in C# and .NET. 

It is built from the ground up on the .NET technology. It leverages modern C# features and a clean, extensible architecture for modelling and simulation.

## 🚀 Getting Started (NuGet)

SimNextgen is available on [GitHub Packages - NuGet](https://github.com/gcl-team/SNA/pkgs/nuget/SimNextGenApp). You can install it using the .NET CLI:

```bash
dotnet add package SimNextgenApp
```

## 🚀 Getting Started: Development and Local Testing

To use SNA in your own project, you can reference it directly from the source code. It's easy!

### 1. Clone the Repository

First, get the SimNextgenApp source code onto your local machine.

```bash
git clone https://github.com/[YourGitHubUsername]/SimNextgenApp.git
```

### 2. Reference the Project

There are two common ways to add the project reference to your solution.

**Method A: Using Visual Studio**

1. Open your own solution (.sln) in Visual Studio.
2. In the Solution Explorer, right-click on your Solution and choose Add -> Existing Project....
3. Navigate to the cloned SimNextgenApp folder and select SimNextgenApp.csproj.
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

## 🧠 Core Concepts

Understanding these concepts will help you master SimNextgen.

- SimulationEngine ⚙️: The central orchestrator of the simulation. It owns the master clock, the Future Event List (FEL), and executes the main event loop. It acts as the concrete implementation for the core simulation logic.
- IScheduler ✉️: A focused interface that provides the capability to schedule new events. The `SimulationEngine` gives an `IScheduler` reference to the `ISimulationModel` during initialisation. This decouples our model from the engine, allowing model components to schedule events (e.g., "a customer arrives in 5 minutes") without needing to know how the event list is implemented.
- IRunContext 🔎: A read-only view of the simulation current state, providing access to the `ClockTime` and `ExecutedEventCount`. The SimulationEngine provides this context to the IRunStrategy, giving it the information needed to decide when the simulation should end.
- IRunStrategy 🏁: A strategy object that tells the `SimulationEngine` when to stop. Because it operates on the clean `IRunContext`, we can create termination conditions based on simulation time (`DurationRunStrategy`), event counts (`EventCountRunStrategy`), or complex system states (`ConditionalRunStrategy`) without being tightly coupled to the engine.
- ISimulationModel & Components 🧱: A model is a container for our simulation interconnected components (`Generator`, `Queue`, `Server`). The model is responsible for setting up the initial state and scheduling the first events when its `Initialize` method is called by the engine.
- AbstractEvent ⚡: The fundamental unit of action in the simulation. Events are simple objects with an `Execute` method. When the `SimulationEngine` processes an event, it advances the simulation clock to the event time and calls its `Execute` method, which in turn modifies the state of our model components and often schedules new future events.

## 🤝 How to Contribute

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

Bug reports and contributions are welcome at [Project Issues](https://github.com/gcl-team/SNA/issues).

## 📜 License
Distributed under the MIT License. See [LICENSE](https://github.com/gcl-team/SNA/blob/main/LICENSE) for more information.
