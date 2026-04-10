#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Compare PostgreSQL connection pool performance across different pool sizes.

.DESCRIPTION
    This script runs the azure-pgsql-pooling simulation in Session pooling mode
    with varying pool sizes to determine optimal configuration.

.PARAMETER Duration
    Duration in seconds for each pool size test (default: 180)

.PARAMETER Series
    Azure instance series (default: "B")

.PARAMETER Size
    Azure instance size (default: "2ms")

.PARAMETER InitialCredits
    Initial CPU credits (default: 60)

.PARAMETER PoolSizes
    Array of pool sizes to test (default: 5,10,20,50,100)

.EXAMPLE
    ./pool-size-comparison.ps1
    Run comparison with default settings

.EXAMPLE
    ./pool-size-comparison.ps1 -Duration 300 -PoolSizes 10,25,50
    Run with custom duration and pool sizes
#>

param(
    [int]$Duration = 180,
    [string]$Series = "B",
    [string]$Size = "2ms",
    [int]$InitialCredits = 60,
    [int[]]$PoolSizes = @(5, 10, 20, 50, 100)
)

# Color output functions
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

Write-ColorOutput "╔════════════════════════════════════════════════════════════════╗" "Cyan"
Write-ColorOutput "║     PostgreSQL Pool Size Optimization Analysis                ║" "Cyan"
Write-ColorOutput "╚════════════════════════════════════════════════════════════════╝" "Cyan"
Write-Host ""
Write-ColorOutput "Configuration:" "Blue"
Write-Host "  • Instance: Azure $Series.$Size"
Write-Host "  • Duration per size: $Duration seconds"
Write-Host "  • Pool sizes: $($PoolSizes -join ', ')"
Write-Host "  • Initial Credits: $InitialCredits"
Write-Host "  • Pooling Mode: Session (optimal for comparison)"
Write-Host ""

$OverallStart = Get-Date

# Create output directory
$OutputDir = "./output/pool_size_comparison"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Function to run a single pool size
function Run-PoolSize {
    param([int]$PoolSize)

    Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"
    Write-ColorOutput "Testing Pool Size: $PoolSize" "Cyan"
    Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"

    $PoolStart = Get-Date

    # Run simulation with session pooling
    dotnet run --project ../SimNextgenApp.Demo.csproj -- demo azure-pgsql-pooling `
        --mode session `
        --pool-size $PoolSize `
        --series $Series `
        --size $Size `
        --duration $Duration `
        --initial-credits $InitialCredits

    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "Simulation failed for pool size $PoolSize" "Red"
        return $false
    }

    # Backup results
    $PoolDir = "$OutputDir/pool_$PoolSize"
    New-Item -ItemType Directory -Force -Path $PoolDir | Out-Null

    if (Test-Path "./output/simulation_latency.csv") {
        Copy-Item "./output/simulation_latency.csv" "$PoolDir/simulation_latency.csv"
        Write-ColorOutput "✓ Backed up latency data" "Green"
    }
    if (Test-Path "./output/simulation_credits.csv") {
        Copy-Item "./output/simulation_credits.csv" "$PoolDir/simulation_credits.csv"
        Write-ColorOutput "✓ Backed up credits data" "Green"
    }

    $PoolElapsed = (Get-Date) - $PoolStart
    Write-ColorOutput "✓ Pool size $PoolSize completed ($([int]$PoolElapsed.TotalSeconds)s)" "Green"
    Write-Host ""
    return $true
}

# Run all pool sizes
foreach ($PoolSize in $PoolSizes) {
    Run-PoolSize -PoolSize $PoolSize
}

# Generate summary statistics
Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"
Write-ColorOutput "Generating Summary Statistics" "Cyan"
Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"

# Create summary data structures
$SummaryData = @()

foreach ($PoolSize in $PoolSizes) {
    $CsvFile = "$OutputDir/pool_$PoolSize/simulation_latency.csv"
    if (Test-Path $CsvFile) {
        $Data = Import-Csv $CsvFile
        $Latencies = $Data | ForEach-Object { [double]$_.'Latency (ms)' }

        if ($Latencies.Count -gt 0) {
            $SortedLatencies = $Latencies | Sort-Object
            $Count = $SortedLatencies.Count

            $Stats = [PSCustomObject]@{
                PoolSize = $PoolSize
                AvgLatency = [Math]::Round(($Latencies | Measure-Object -Average).Average, 2)
                MinLatency = [Math]::Round(($Latencies | Measure-Object -Minimum).Minimum, 2)
                MaxLatency = [Math]::Round(($Latencies | Measure-Object -Maximum).Maximum, 2)
            }

            $SummaryData += $Stats
        }
    }
}

# Export detailed summary
$SummaryData | Export-Csv "$OutputDir/latency_vs_pool_size.csv" -NoTypeInformation
Write-ColorOutput "✓ Created latency_vs_pool_size.csv" "Green"

# Export simplified summary for charts
$SummaryData | Select-Object @{Name='Pool Size';Expression={$_.PoolSize}}, @{Name='Average Latency (ms)';Expression={$_.AvgLatency}} |
    Export-Csv "$OutputDir/latency_summary.csv" -NoTypeInformation
Write-ColorOutput "✓ Created latency_summary.csv" "Green"

# Check if graph-cli is available
$GraphAvailable = $null -ne (Get-Command graph -ErrorAction SilentlyContinue)

if (-not $GraphAvailable) {
    Write-ColorOutput "graph-cli not found. Install with: pip install graph-cli" "Yellow"
    Write-ColorOutput "Skipping graph generation" "Yellow"
    Write-ColorOutput "(CSV files are still available for manual plotting)" "Yellow"
} else {
    Write-ColorOutput "Generating comparison charts..." "Blue"

    # Generate line chart: Average Latency vs Pool Size
    graph "$OutputDir/latency_summary.csv" `
        --title "Average Latency vs Pool Size (Session Pooling)" `
        --xlabel "Pool Size" `
        --ylabel "Latency (ms)" `
        -o "$OutputDir/latency_vs_pool_size.png"
    Write-ColorOutput "✓ Generated latency_vs_pool_size.png (line chart)" "Green"

    # Generate bar chart for easier reading (full scale from 0)
    graph "$OutputDir/latency_summary.csv" `
        --title "Average Latency vs Pool Size (Session Pooling)" `
        --xlabel "Pool Size" `
        --ylabel "Latency (ms)" `
        --bar `
        --bar-label `
        -o "$OutputDir/latency_bar_chart.png"
    Write-ColorOutput "✓ Generated latency_bar_chart.png (full scale)" "Green"

    # Generate zoomed bar chart (emphasizes differences)
    $MinLatency = ($SummaryData | Measure-Object -Property AvgLatency -Minimum).Minimum
    $MaxLatency = ($SummaryData | Measure-Object -Property AvgLatency -Maximum).Maximum
    $RangeMin = $MinLatency - 5
    $RangeMax = $MaxLatency + 5

    graph "$OutputDir/latency_summary.csv" `
        --bar `
        --bar-label `
        --title "Average Latency vs Pool Size (Zoomed)" `
        --xlabel "Pool Size" `
        --ylabel "Latency (ms)" `
        --yrange "$RangeMin`:$RangeMax" `
        -o "$OutputDir/latency_bar_chart_zoomed.png"
    Write-ColorOutput "✓ Generated latency_bar_chart_zoomed.png (emphasizes differences)" "Green"

    # Generate individual time series for each pool size
    foreach ($PoolSize in $PoolSizes) {
        $CsvFile = "$OutputDir/pool_$PoolSize/simulation_latency.csv"
        if (Test-Path $CsvFile) {
            $Data = Import-Csv $CsvFile
            $MaxLatency = ($Data | ForEach-Object { [double]$_.'Latency (ms)' } | Measure-Object -Maximum).Maximum

            graph $CsvFile `
                --title "Latency Time Series - Pool Size $PoolSize" `
                --color "blue" `
                --yrange "0:$MaxLatency" `
                -o "$OutputDir/pool_$PoolSize/latency_timeseries.png"
        }
    }
    Write-ColorOutput "✓ Generated individual time series charts" "Green"
}

# Display summary table
Write-Host ""
Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"
Write-ColorOutput "Summary: Latency vs Pool Size" "Cyan"
Write-ColorOutput "═══════════════════════════════════════════════════════════════" "Cyan"

Write-Host $("{0,-12} {1,-15} {2,-15} {3,-15}" -f "Pool Size", "Avg Latency", "Min Latency", "Max Latency")
Write-Host "────────────────────────────────────────────────────────────────────"

foreach ($Stats in $SummaryData) {
    Write-Host $("{0,-12} {1,10:F2} ms   {2,10:F2} ms   {3,10:F2} ms" -f `
        $Stats.PoolSize, $Stats.AvgLatency, $Stats.MinLatency, $Stats.MaxLatency)
}

Write-Host ""
Write-ColorOutput "Recommendations:" "Blue"

# Find optimal pool size (lowest average latency)
$OptimalStats = $SummaryData | Sort-Object AvgLatency | Select-Object -First 1
Write-ColorOutput "  • Optimal pool size: $($OptimalStats.PoolSize) (avg latency: $($OptimalStats.AvgLatency)ms)" "Green"

# Check for diminishing returns (when improvement < 5%)
for ($i = 1; $i -lt $SummaryData.Count; $i++) {
    $PrevAvg = $SummaryData[$i-1].AvgLatency
    $CurrentAvg = $SummaryData[$i].AvgLatency
    $Improvement = ($PrevAvg - $CurrentAvg) / $PrevAvg * 100

    if ($Improvement -lt 0) {
        Write-ColorOutput "  • Pool size $($SummaryData[$i].PoolSize): Performance degraded ($([Math]::Round($Improvement, 2))% worse)" "Yellow"
    } elseif ($Improvement -lt 5) {
        Write-ColorOutput "  • Pool size $($SummaryData[$i].PoolSize): Diminishing returns (<5% improvement)" "Yellow"
    }
}

$OverallElapsed = (Get-Date) - $OverallStart
Write-Host ""
Write-ColorOutput "╔════════════════════════════════════════════════════════════════╗" "Green"
Write-ColorOutput "║    Pool Size Comparison Complete! (Total: $([int]$OverallElapsed.TotalSeconds)s)           ║" "Green"
Write-ColorOutput "╚════════════════════════════════════════════════════════════════╝" "Green"
Write-Host ""
Write-Host "Results saved in: " -NoNewline
Write-ColorOutput "$OutputDir" "Blue"
