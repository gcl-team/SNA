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

## 🌐 Running the Demo on Non-Windows or Non-Visual Studio Environments

If we do not have Visual Studio or we are using a Linux or macOS machine with .NET installed, 
we can run the demo directly from the command line.

### Requirements

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) to build or run from source.

### Step 1: Running the demo

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

## 🐳 Running the Demo with Docker

If you have Docker installed, you can build and run the demo inside a container without needing .NET or Visual Studio installed locally.

### Step 1: Build the Docker image

From the folder containing the Dockerfile, run:

```bash
docker build -t sna-demo .
```

### Step 2: Run different demo scenarios inside the container

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

### Step 3: (Optional) Publish to Docker Hub

If you want to share the Docker image, you can push it to Docker Hub or any other container registry.

```bash
docker login

docker tag sna-demo your-dockerhub-username/sna-demo:latest

docker push your-dockerhub-username/sna-demo:latest
```

## ☁️ Running the Demo in Kubernetes

If you have a Kubernetes cluster running (e.g., via Minikube or Docker Desktop), you can run the `SimNextgenApp.Demo` demo as a batch job using the built Docker image.

> ✅ This is great for showcasing how our DES simulations can run in the cloud or as part of automated workflows.

### Step 1: Apply the Kubernetes Job

Locate the `sna-simple-server-job.yaml` file in the `SimNextgenApp.Demo` folder. 
This file defines a Kubernetes job that runs the simple server demo.

```bash
kubectl apply -f sna-simple-server-job.yaml
```

### Step 2: Check the Job Status

Check if the job has completed:

```bash
kubectl get jobs
```

Get the logs of the job to see the output:

```bash
kubectl logs job/sna-simple-server-5s
```

This example runs a single simulation with `--arrival-secs 5.0`. We can easily modify the 
`args` section in the YAML to test different parameters or scenarios.

> 💡 Tip: Use unique `metadata.name` if you run multiple jobs, or `kubectl delete job sna-simple-server-5s` to clean up.

## 🔁 Running Batch Simulations with Argo Workflows

We can run multiple simulations with varying parameters (e.g., different arrival times) using Argo Workflows, a powerful tool for orchestrating Kubernetes-native jobs.

### Requirements

We need a Kubernetes cluster with Argo Workflows installed. We can follow the [Argo Workflows installation guide](https://argoproj.github.io/argo-workflows/quick-start/) to set it up.

To verify if Argo is installed, run:
```bash
kubectl get crds | grep workflow
```

If you see `workflows.argoproj.io`, Argo is ready to use.

### Step 1: Create an Argo Workflow YAML

We have a sample workflow file `sna-simple-server-sweep.yaml` that defines multiple runs of the simple server demo with different parameters.

### Step 2: Submit the Workflow

We can submit the workflow using the following command:
```bash
kubectl create -f sna-simple-server-sweep.yaml
```

To monitor the workflow, we can run:
```bash
kubectl get workflows
```

Once a workflow is running, get its pod logs using:
```bash
kubectl get pods -l workflows.argoproj.io/workflow=<workflow name here>

kubectl logs <pod name here>
```

> 💡 Tip: To clean up finished workflows and their pods, you can delete them with:
> ```bash
> kubectl delete workflow <workflow name here>
> ```
