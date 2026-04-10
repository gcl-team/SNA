<#
.SYNOPSIS
    Run the SNA simulation and optionally plot results.
.DESCRIPTION
    This script builds the SimNextgenApp.Demo project and runs the AWS RDS simulation with the provided arguments.
    Supported instance types include t3, t4g, and m5 families with various sizes.
    After the simulation completes, it generates graphs for credits and latency if the graph-cli tool is installed.

    GRAFANA CLOUD INTEGRATION:
    Use --grafana true to export metrics to Grafana Cloud via OpenTelemetry.
    Requires GRAFANA_API_KEY environment variable (format: INSTANCE_ID:API_TOKEN).
    Set the API key before running the script and use the example below as a reference for configuration.
.EXAMPLE
    # Basic simulation with CSV export
    ./simulation.ps1 aws-rds-burst --family t3 --size medium --duration 720 --initial-credits 10
.EXAMPLE
    # With unlimited credits
    ./simulation.ps1 aws-rds-burst --family t4g --size large --duration 1200 --unlimited-credits true
.EXAMPLE
    # With Grafana Cloud export
    $env:GRAFANA_API_KEY = "123456:glc_abc123..."
    ./simulation.ps1 aws-rds-burst --family t3 --size medium --duration 720 --grafana true
.LINK
    https://github.com/gcl-team/SNAS
#>

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    $RemainingArgs
)

# Resolve script directory and cd into it to ensure relative paths work
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "Building project and starting SNA simulation..." -ForegroundColor Cyan

$startTime = Get-Date

dotnet run --project ../SimNextgenApp.Demo.csproj -- demo $RemainingArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Simulation failed" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Generating graphs..." -ForegroundColor Green
    
# Check if graph-cli is installed
if (-not (Get-Command graph -ErrorAction SilentlyContinue)) {
    Write-Host "graph-cli not found. Please run: pip install graph-cli" -ForegroundColor Yellow
} else {
    # Generate credits graph only if credits CSV exists (burstable instances only)
    if (Test-Path ./output/simulation_credits.csv) {
        $credits = Import-Csv ./output/simulation_credits.csv
        $creditsMax = ($credits | Measure-Object -Property "Credits" -Maximum).Maximum
        $surplusMax = ($credits | Measure-Object -Property "Surplus Credit Debt" -Maximum).Maximum

        # Normalize nulls to 0 before calling Math::Max to handle empty CSV edge case
        if ($null -eq $creditsMax) { $creditsMax = 0 }
        if ($null -eq $surplusMax) { $surplusMax = 0 }

        $creditMax = [Math]::Max($creditsMax, $surplusMax)
        if ($creditMax -eq 0) { $creditMax = 1 }

        graph ./output/simulation_credits.csv --title "AWS RDS Credits (Regular vs Surplus)" --yrange="0:$creditMax" -o ./output/simulation_credits.png
        Write-Host "Credits graph generated" -ForegroundColor Green
    } else {
        Write-Host "Skipping credits graph (not a burstable instance)" -ForegroundColor Yellow
    }

    # Generate latency graph if CSV exists
    if (Test-Path ./output/simulation_latency.csv) {
        $latency = Import-Csv ./output/simulation_latency.csv
        # Use index-based property access since header has parentheses
        $latencyMax = ($latency | ForEach-Object { $_."Latency (ms)" } | Measure-Object -Maximum).Maximum
        if ($null -eq $latencyMax) { $latencyMax = 1 }

        graph ./output/simulation_latency.csv --title "Simulation Latency" --yrange="0:$latencyMax" -o ./output/simulation_latency.png
        Write-Host "Latency graph generated" -ForegroundColor Green
    } else {
        Write-Host "Latency CSV not found" -ForegroundColor Yellow
    }
}

$elapsedTime = (Get-Date) - $startTime
$elapsedSeconds = [int]$elapsedTime.TotalSeconds
Write-Host "Simulation completed successfully! (Duration: $elapsedSeconds`s)" -ForegroundColor Green