<#
.SYNOPSIS
    Run the SNA simulation and optionally plot results.
.DESCRIPTION
    This script builds the SimNextgenApp.Demo project and runs the AWS RDS simulation with the provided arguments.
    Supported instance types include t3.medium, t4g.medium, and m5.large.
    After the simulation completes, it generates graphs for credits and latency if the graph-cli tool is installed.
.EXAMPLE
    ./simulation.ps1 aws-rds-burst --family t3 --size medium --duration 720 --initial-credits 10 --unlimited-credits false
.EXAMPLE
    ./simulation.ps1 aws-rds-burst --family t4g --size large --duration 1200
.LINK
    https://github.com/gcl-team/SNAS
#>

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    $RemainingArgs
)

Write-Host "Building project and starting SNA simulation..." -ForegroundColor Cyan

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
    graph ./output/simulation_credits.csv --title "Simulation Credits" -o ./output/simulation_credits.png
    graph ./output/simulation_latency.csv --title "Simulation Latency" -o ./output/simulation_latency.png
}

Write-Host "Simulation completed successfully!" -ForegroundColor Green