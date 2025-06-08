# SNA

## 👋 Welcome to SNA!

SNA is a lightweight open-source library for building Discrete Event Simulations (DES) in C# and .NET. 

It is built from the ground up on the .NET technology. It leverages modern C# features and a clean, extensible architecture for modelling and simulation.

## 🚀 Getting Started (Using the Source)

This project is in its early stages and is not yet published to NuGet. To use SNA in your own project, you can reference it directly from the source code. It's easy!

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

For a more detailed example, check out our SimNextgenApp.Demo project!

## 🧠 Core Concepts

Understanding these concepts will help you master SimNextgen.

- SimulationEngine ⚙️: The heart of the library. It manages the simulation clock and the event loop.
- ISimulationModel & Components 🧱: A model is a container for your simulation's components (Generator, Queue, Server). You define the structure and connections of your system here.
- IRunStrategy 🏁: A strategy object that tells the SimulationEngine when to stop. This decouples the run logic from the engine itself.
- Statistics 📈: Components have built-in collectors for key metrics like wait times, utilization, and queue length.

## 🤝 How to Contribute

Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

We are excited to welcome contributors! Whether you are fixing a bug, proposing a new feature, or improving our documentation, we'd love to have your help.

1. Fork the Project
2. Create your Feature Branch (git checkout -b feature/AmazingFeature)
3. Commit your Changes (git commit -m 'Add some AmazingFeature')
4. Push to the Branch (git push origin feature/AmazingFeature)
5. Open a Pull Request

Bug reports and contributions are welcome at [Project Issues](https://github.com/gcl-team/SNA/issues).

## 📜 License
Distributed under the MIT License. See [LICENSE](https://github.com/gcl-team/SNA/blob/main/LICENSE) for more information.
