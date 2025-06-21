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
