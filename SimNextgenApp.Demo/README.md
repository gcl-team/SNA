# SNA Demos

## 🧪 Running the Demo in Visual Studio

We can easily run different demo scenarios using Visual Studio Debug Profiles:

1. Open the debug target dropdown near the green ▶️ (Start) button.
2. We shall see multiple profiles listed, for example:
   - Simple Generator Demo;
   - Simple Server Demo;
   - Simple M/M/c/K Demo.
3. Select the demo profile we want to run.
4. Press F5 (Start Debugging) to launch the selected demo with pre-configured parameters.

To add or modify demo runs, open the ``launchSettings.json`` file in the Properties folder. 
Each profile defines the command line arguments for a demo run.

## 🐳 Running the Demo with Docker

If you have Docker installed, you can build and run the demo inside a container without needing .NET or Visual Studio installed locally.

### Build the Docker image

From the folder containing the Dockerfile, run:

```bash
docker build -t sna-demo .
```

### Run different demo scenarios inside the container

You can pass command-line arguments to the container just like running the app locally. For example:

```bash
docker run --rm sna-demo demo simple-generator
```

```bash
docker run --rm sna-demo demo simple-server --arrival-secs 5.0
```

```bash
docker run --rm sna-demo demo mmck --servers 3 --capacity 10 --arrival-secs 2.5
```

The logs and output will appear in our terminal.

## 🌐 Running the Demo on Non-Windows or Non-Visual Studio Environments

If we do not have Visual Studio or we are using a Linux or macOS machine with .NET installed, 
we can run the demo directly from the command line.

### Requirements

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) to build or run from source.

### Running the demo

Navigate to the folder containing the built DLL (SimNextgenApp.Demo.dll) and run:

```bash
dotnet SimNextgenApp.Demo.dll demo simple-generator
```

or for other scenarios:

```bash
dotnet SimNextgenApp.Demo.dll demo simple-server --arrival-secs 5.0
```

```bash
dotnet SimNextgenApp.Demo.dll demo mmck --servers 3 --capacity 10 --arrival-secs 2.5
```