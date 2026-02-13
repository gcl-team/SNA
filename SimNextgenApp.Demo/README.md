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

For AWS RDS Sample, you can choose to run the following scripts in the `AwsRdsSample` directory.

**Windows (PowerShell):**
```powershell
./simulation.ps1 aws-rds-burst --family t3 --size medium --duration 720 --initial-credits 10 --unlimited-credits false
```

**macOS/Linux (Bash):**
```bash
./simulation.sh aws-rds-burst --family t4g --size medium --duration 720
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

### Step 3: (Optional) View Logs on Seq

Follow [the official tutorial](https://datalust.co/docs/getting-started) to run Seq in a Docker container:

```bash
docker run \
  --name seq \
  -d \
  --restart unless-stopped \
  -e ACCEPT_EULA=Y \
  -e SEQ_FIRSTRUN_ADMINPASSWORD=<password> \
  -v <local path to store data>:/data \
  -p 5341:80 \
  datalust/seq
```

Dashboard (Localhost): http://localhost:8081/

Demo Query: 

```sql
select @Timestamp, coalesce(@Data.CustomerGroupSize, 0) as CustomerGroupSize, @Data.AvailableTable as AvailableTableCount 
from stream 
where @Data.AvailableTable is not null and ProfileRunId = '<profile run id>'
```

### Step 4: (Optional) Publish to Docker Hub

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
kubectl get crds
```

If you see `workflows.argoproj.io`, Argo is ready to use.

### Step 1: Create an Argo Workflow YAML

We have a sample workflow file `sna-simple-server-sweep.yaml` that defines multiple runs of the simple server demo with different parameters.

### Step 2: Submit the Workflow

We can submit the workflow using the following command:
```bash
kubectl create -f sna-simple-server-sweep.yaml -n argo
```

To monitor the workflow, we can run:
```bash
kubectl get workflows -n argo
```

Once a workflow is running, get its pod logs using:
```bash
kubectl get pods -l workflows.argoproj.io/workflow=<workflow name here> -n argo

kubectl logs <pod name here> -n argo
```

> 💡 Tip: To clean up finished workflows and their pods, you can delete them with:
> ```bash
> kubectl delete workflow <workflow name here> -n argo
> ```

## 🖼️ Using Argo Workflows UI

The Argo Workflows UI provides a powerful web-based dashboard to visualise and manage our workflows. 
This guide covers how to access it and troubleshoot common initial setup issues.

### Step 1: Access the UI via Port Forwarding

The easiest way to access the UI from our local machine is to create a secure tunnel to the `argo-server` service running 
in our Kubernetes cluster.

1.  Open a terminal and run the following command. This will block the terminal while the tunnel is active.

    ```bash
    kubectl -n argo port-forward svc/argo-server 2746:2746
    ```

2.  Open the web browser and navigate to: **`https://localhost:2746`**

> **Important:** We must use `https://`. The Argo Server uses a self-signed certificate by default, so the browser 
will show a security warning like "Your connection is not private". This is expected. 
Click "Advanced" and then "Proceed to localhost (unsafe)" to continue. 
If we use `http://`, we will likely get an `ERR_EMPTY_RESPONSE` error.

### Step 2: Logging In

The UI uses Kubernetes Service Account tokens for authentication. We thus need to generate a token to log in.

1.  Open a **new terminal window** (leave the port-forward running).
2.  Run the following `kubectl` command to generate a token for the `argo-server` service account:

    ```bash
    kubectl create token argo-server -n argo
    ```

3.  Copy the entire long string (the token).
4.  Paste this token into the login text box in the Argo UI and click "LOGIN".

### Troubleshooting: "Unauthorized" Error

This is the most common issue after the initial login.

The `argo-server` service account needs permission to view workflow resources in the cluster. 
For local development, the quickest fix is to grant it `cluster-admin` rights with the file `argo-cluster-admin.yaml`:

   ```bash
   kubectl apply -f argo-server-admin.yaml
   ```

If you still get "Unauthorized" after applying the permissions, the Argo Server may be misconfigured. 
It needs to be in `server` auth mode to correctly use the permissions of the token we provide. 
We can then patch the live `argo-server` deployment to explicitly set the correct mode. 
This command will trigger a restart of the server pod.

   ```bash
   kubectl -n argo patch deployment argo-server --type='json' -p='[{"op": "add", "path": "/spec/template/spec/containers/0/args/1", "value": "--auth-mode=server"}]'
   ```

After that we wait for the new `argo-server` pod to be in the `1/1 Running` state. 
We can watch the progress with 

   ```bash
   kubectl -n argo get pods -w
   ```

After applying, you may find that the Argo UI **no longer prompts you to log in at all**.

This is the expected outcome of this configuration. The `argo-server` is now running as a privileged user (`cluster-admin`) and 
is configured to check permissions correctly. The system recognises that the server itself is a trusted superuser, so it grants 
you access directly without needing a separate user token.