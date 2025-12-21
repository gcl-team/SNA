<#
.SYNOPSIS
    Run the SNA simulation and optionally plot results.
.EXAMPLE
    ./simulation.ps1 aws-rds-t3-burst --duration 720 --initial-credits 10 --unlimited-credits false
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