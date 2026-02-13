<#
.SYNOPSIS
    Run the SNA simulation and optionally plot results.
.DESCRIPTION
    This script builds the SimNextgenApp.Demo project and runs the AWS RDS simulation with the provided arguments.
    Supported instance types include t3, t4g, and m5 families with various sizes.
    After the simulation completes, it generates graphs for credits and latency if the graph-cli tool is installed.
.EXAMPLE
    ./simulation.ps1 aws-rds-burst --family t3 --size medium --duration 720 --initial-credits 10 --unlimited-credits true
.EXAMPLE
    ./simulation.ps1 aws-rds-burst --family t4g --size large --duration 1200 --unlimited-credits true
.LINK
    https://github.com/gcl-team/SNAS
#>

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    $RemainingArgs
)

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
    # Calculate Max for scaling
    $credits = Import-Csv ./output/simulation_credits.csv
    $creditMax = ($credits | Measure-Object -Property "Credits" -Maximum).Maximum
    if ($null -eq $creditMax) { $creditMax = 1 }

    $latency = Import-Csv ./output/simulation_latency.csv
    # Use index-based property access since header has parentheses
    $latencyMax = ($latency | ForEach-Object { $_."Latency (ms)" } | Measure-Object -Maximum).Maximum
    if ($null -eq $latencyMax) { $latencyMax = 1 }

    graph ./output/simulation_credits.csv --title "Simulation Credits" --yrange="0:$creditMax" -o ./output/simulation_credits.png
    graph ./output/simulation_latency.csv --title "Simulation Latency" --yrange="0:$latencyMax" -o ./output/simulation_latency.png
}

Write-Host "Simulation completed successfully!" -ForegroundColor Green