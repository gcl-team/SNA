<#
.SYNOPSIS
    Run PostgreSQL connection pooling comparison across all three modes.
.DESCRIPTION
    This script runs the azure-pgsql-pooling simulation for Direct, Session, and Transaction modes,
    backs up the CSV results, and generates comparison graphs using graph-cli.
.EXAMPLE
    # Run comparison with default settings (120 seconds, pool size 20)
    ./pooling-comparison.ps1
.EXAMPLE
    # Run with custom duration and pool size
    ./pooling-comparison.ps1 -Duration 300 -PoolSize 30
.PARAMETER Duration
    Simulation duration in seconds (default: 120)
.PARAMETER PoolSize
    Connection pool size (default: 20)
.PARAMETER Series
    Azure instance series (default: B)
.PARAMETER Size
    Azure instance size (default: 2ms)
.PARAMETER InitialCredits
    Initial CPU credits (default: 60)
.LINK
    https://github.com/gcl-team/SNAS
#>

param(
    [int]$Duration = 120,
    [int]$PoolSize = 20,
    [string]$Series = "B",
    [string]$Size = "2ms",
    [int]$InitialCredits = 60
)

# Resolve script directory and cd into it to ensure relative paths work
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     PostgreSQL Connection Pooling Comparison                  ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Blue
Write-Host "  • Instance: Azure $Series.$Size"
Write-Host "  • Duration: $Duration seconds"
Write-Host "  • Pool Size: $PoolSize"
Write-Host "  • Initial Credits: $InitialCredits"
Write-Host ""

$modes = @(
    @{ Name = "direct"; Label = "Direct Connections (50ms overhead)" },
    @{ Name = "session"; Label = "Session Pooling (no overhead)" },
    @{ Name = "transaction"; Label = "Transaction Pooling (8ms overhead)" }
)

$overallStart = Get-Date

# Create backup directory
New-Item -ItemType Directory -Force -Path "./output/pooling_comparison" | Out-Null

# Function to run a single mode
function Run-Mode {
    param(
        [string]$Mode,
        [string]$ModeLabel
    )

    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "Running: $ModeLabel" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

    $modeStart = Get-Date

    # Run simulation
    dotnet run --project ../SimNextgenApp.Demo.csproj -- demo azure-pgsql-pooling `
        --mode $Mode `
        --pool-size $PoolSize `
        --series $Series `
        --size $Size `
        --duration $Duration `
        --initial-credits $InitialCredits

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Simulation failed for $Mode" -ForegroundColor Red
        return $false
    }

    # Backup results
    $modeDir = "./output/pooling_comparison/$Mode"
    New-Item -ItemType Directory -Force -Path $modeDir | Out-Null

    if (Test-Path ./output/simulation_latency.csv) {
        Copy-Item ./output/simulation_latency.csv "$modeDir/simulation_latency.csv"
        Write-Host "✓ Backed up latency data" -ForegroundColor Green
    }
    if (Test-Path ./output/simulation_credits.csv) {
        Copy-Item ./output/simulation_credits.csv "$modeDir/simulation_credits.csv"
        Write-Host "✓ Backed up credits data" -ForegroundColor Green
    }

    $modeElapsed = ((Get-Date) - $modeStart).TotalSeconds
    Write-Host "✓ $ModeLabel completed ($([int]$modeElapsed)s)" -ForegroundColor Green
    Write-Host ""

    return $true
}

# Run all three modes
foreach ($modeInfo in $modes) {
    $result = Run-Mode -Mode $modeInfo.Name -ModeLabel $modeInfo.Label
    if (-not $result) {
        Write-Host "Exiting due to error" -ForegroundColor Red
        exit 1
    }
}

# Generate comparison graphs
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Generating Median Latency Comparison Charts" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# Check if graph-cli is available
if (-not (Get-Command graph -ErrorAction SilentlyContinue)) {
    Write-Host "graph-cli not found. Install with: pip install graph-cli" -ForegroundColor Yellow
    Write-Host "Skipping graph generation" -ForegroundColor Yellow
    Write-Host "(CSV files are still available in ./output/pooling_comparison/)" -ForegroundColor Yellow
} else {
    Write-Host "Generating median latency bar charts..." -ForegroundColor Blue

    # Load simulation data
    $directData = Import-Csv "./output/pooling_comparison/direct/simulation_latency.csv"
    $sessionData = Import-Csv "./output/pooling_comparison/session/simulation_latency.csv"
    $transactionData = Import-Csv "./output/pooling_comparison/transaction/simulation_latency.csv"

    # Helper function to calculate median
    function Get-Median {
        param([double[]]$values)
        $sorted = $values | Sort-Object
        $count = $sorted.Count
        if ($count -eq 0) { return 0 }
        $mid = [math]::Floor($count / 2)
        if ($count % 2 -eq 1) {
            return $sorted[$mid]
        } else {
            return ($sorted[$mid - 1] + $sorted[$mid]) / 2
        }
    }

    # Calculate median latencies
    $directMedian = Get-Median ($directData | ForEach-Object { [double]$_."Latency (ms)" })
    $sessionMedian = Get-Median ($sessionData | ForEach-Object { [double]$_."Latency (ms)" })
    $transactionMedian = Get-Median ($transactionData | ForEach-Object { [double]$_."Latency (ms)" })

    # Create summary CSV for bar chart
    $summaryCsv = @"
Mode,Median Latency (ms)
Direct,$([math]::Round($directMedian, 2))
Session,$([math]::Round($sessionMedian, 2))
Transaction,$([math]::Round($transactionMedian, 2))
"@
    $summaryCsv | Out-File "./output/pooling_comparison/latency_summary.csv" -Encoding UTF8

    # Generate bar chart (full scale from 0)
    graph "./output/pooling_comparison/latency_summary.csv" --bar --title "Median Latency Comparison (P50)" --ylabel "Latency (ms)" -o "./output/pooling_comparison/latency_bar_chart.png"
    Write-Host "✓ Generated latency_bar_chart.png (full scale)" -ForegroundColor Green

    # Generate zoomed bar chart (emphasizes differences)
    $summaryData = Import-Csv "./output/pooling_comparison/latency_summary.csv"
    $latencies = $summaryData | ForEach-Object { [double]$_."Median Latency (ms)" }
    $minLatency = ($latencies | Measure-Object -Minimum).Minimum
    $maxLatency = ($latencies | Measure-Object -Maximum).Maximum
    $rangeMin = [math]::Floor($minLatency - 1.0)
    $rangeMax = [math]::Ceiling($maxLatency + 1.0)

    graph "./output/pooling_comparison/latency_summary.csv" --bar --title "Median Latency Comparison (Zoomed)" --ylabel "Latency (ms)" --yrange="$rangeMin`:$rangeMax" -o "./output/pooling_comparison/latency_bar_chart_zoomed.png"
    Write-Host "✓ Generated latency_bar_chart_zoomed.png (emphasizes differences)" -ForegroundColor Green
}

# Calculate and display summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

$summaryFormat = "{0,-30} {1,15} {2,15} {3,15}"
Write-Host ($summaryFormat -f "Mode", "Median (P50)", "Min Latency", "Max Latency")
Write-Host "───────────────────────────────────────────────────────────────────────────"

foreach ($modeInfo in $modes) {
    $mode = $modeInfo.Name
    $csvFile = "./output/pooling_comparison/$mode/simulation_latency.csv"

    if (Test-Path $csvFile) {
        $data = Import-Csv $csvFile
        $latencies = $data | ForEach-Object { [double]$_."Latency (ms)" }

        $median = Get-Median $latencies
        $min = ($latencies | Measure-Object -Minimum).Minimum
        $max = ($latencies | Measure-Object -Maximum).Maximum

        $modeLabel = switch ($mode) {
            "direct" { "Direct (50ms overhead)" }
            "session" { "Session Pooling (no overhead)" }
            "transaction" { "Transaction (8ms overhead)" }
        }

        Write-Host ($summaryFormat -f $modeLabel, "$([math]::Round($median, 2)) ms", "$([math]::Round($min, 2)) ms", "$([math]::Round($max, 2)) ms")
    }
}

Write-Host "═══════════════════════════════════════════════════════════════════════════"
Write-Host ""
Write-Host "🏆 RECOMMENDATIONS:" -ForegroundColor Green
Write-Host "   • Session Pooling: " -NoNewline
Write-Host "✅ Best for most OLTP workloads (lowest latency)" -ForegroundColor Green
Write-Host "   • Transaction Pooling: " -NoNewline
Write-Host "⚠️  Use only for serverless/multi-tenant scenarios" -ForegroundColor Yellow
Write-Host "   • Direct Connections: " -NoNewline
Write-Host "❌ Avoid (highest overhead)" -ForegroundColor Red
Write-Host ""

$overallElapsed = ((Get-Date) - $overallStart).TotalSeconds
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Comparison Complete! (Total Duration: $([int]$overallElapsed)s)                ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "Results saved in: " -NoNewline
Write-Host "./output/pooling_comparison/" -ForegroundColor Blue
