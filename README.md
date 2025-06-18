# SNA

## 👋 Welcome to SNA!

SNA is a lightweight open-source library for building Discrete Event Simulations (DES) in C# and .NET. 

It is built from the ground up on the .NET technology. It leverages modern C# features and a clean, extensible architecture for modelling and simulation.

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

## 🚀 Getting Started: Development and Local Testing

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

- SimulationEngine ⚙️: The main manager of the simulation. It keeps track of the simulation time (like a main clock), a list of what's going to happen next (Future Event List or FEL), and runs the simulation step-by-step. This is where the main simulation logic happens.
- IScheduler ✉️: A tool that lets you schedule new events to happen in the future. The `SimulationEngine` gives an `IScheduler` reference to the `ISimulationModel` when things start up. This means your simulation parts can schedule events (like 'a customer arrives in 5 minutes') without needing to know the nitty-gritty details of how the event list works. It keeps your model tidy and separate from the engine's internal workings.
- IRunContext 🔎: A way to look at the current status of the simulation (like the current time and how many events have happened) without being able to change anything. The `SimulationEngine` shows this to the `IRunStrategy`, which uses this info to decide when to stop the simulation.
- IRunStrategy 🏁: A helper that decides when the `SimulationEngine` should end the simulation. Since it just looks at the current status (using `IRunContext`), you can set up rules to stop the simulation based on time (e.g., run for 100 minutes), number of events (e.g., after 1000 events), or more complex conditions, all without getting tangled up with the engine's details.
- ISimulationModel & Components 🧱: Think of a model as the main blueprint for your simulation, holding all its working parts (which we call components, like `Generator`, `Queue`, `Server`). When the simulation starts, the engine calls the model's `Initialize` method, which sets everything up and schedules the very first events to get things rolling.
- AbstractEvent ⚡: The basic building block for anything that happens in the simulation. Events are straightforward instructions: they each have an `Execute` method that says what to do. When it's time for an event, the `SimulationEngine` moves the simulation clock to the event's time, runs its `Execute` method. This usually changes something in your model's components and often lines up new events for the future.

## 🤝 How to Contribute

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

Bug reports and contributions are welcome at [Project Issues](https://github.com/gcl-team/SNA/issues).

## 📜 License
Distributed under the MIT License. See [LICENSE](https://github.com/gcl-team/SNA/blob/main/LICENSE) for more information.
