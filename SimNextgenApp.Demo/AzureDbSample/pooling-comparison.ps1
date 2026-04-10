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

# Merge CSVs and generate comparison graphs
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Merging Results & Generating Comparison Graphs" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

# Merge latency CSVs
$latencyDirect = "./output/pooling_comparison/direct/simulation_latency.csv"
$latencySession = "./output/pooling_comparison/session/simulation_latency.csv"
$latencyTransaction = "./output/pooling_comparison/transaction/simulation_latency.csv"

if ((Test-Path $latencyDirect) -and (Test-Path $latencySession) -and (Test-Path $latencyTransaction)) {
    Write-Host "Merging latency data..." -ForegroundColor Blue

    # Load all three CSV files
    $directData = Import-Csv $latencyDirect
    $sessionData = Import-Csv $latencySession
    $transactionData = Import-Csv $latencyTransaction

    # Create hashtables indexed by rounded time
    $directHash = @{}
    $sessionHash = @{}
    $transactionHash = @{}

    foreach ($row in $directData) {
        $time = [math]::Round([double]$row."Simulation Time (s)", 2)
        $directHash[$time] = $row."Latency (ms)"
    }

    foreach ($row in $sessionData) {
        $time = [math]::Round([double]$row."Simulation Time (s)", 2)
        $sessionHash[$time] = $row."Latency (ms)"
    }

    foreach ($row in $transactionData) {
        $time = [math]::Round([double]$row."Simulation Time (s)", 2)
        $transactionHash[$time] = $row."Latency (ms)"
    }

    # Merge data
    $mergedLatency = @()
    $mergedLatency += "Simulation Time (s),Direct (ms),Session (ms),Transaction (ms)"

    $allTimes = $directHash.Keys | Sort-Object
    foreach ($time in $allTimes) {
        if ($sessionHash.ContainsKey($time) -and $transactionHash.ContainsKey($time)) {
            $mergedLatency += "$time,$($directHash[$time]),$($sessionHash[$time]),$($transactionHash[$time])"
        }
    }

    $mergedLatency | Out-File "./output/pooling_comparison/latency_combined.csv" -Encoding UTF8
    Write-Host "✓ Created latency_combined.csv" -ForegroundColor Green
}

# Merge credits CSVs
$creditsDirect = "./output/pooling_comparison/direct/simulation_credits.csv"
$creditsSession = "./output/pooling_comparison/session/simulation_credits.csv"
$creditsTransaction = "./output/pooling_comparison/transaction/simulation_credits.csv"

if ((Test-Path $creditsDirect) -and (Test-Path $creditsSession) -and (Test-Path $creditsTransaction)) {
    Write-Host "Merging credits data..." -ForegroundColor Blue

    # Load all three CSV files
    $directData = Import-Csv $creditsDirect
    $sessionData = Import-Csv $creditsSession
    $transactionData = Import-Csv $creditsTransaction

    # Create hashtables indexed by rounded time
    $directHash = @{}
    $sessionHash = @{}
    $transactionHash = @{}

    foreach ($row in $directData) {
        $time = [math]::Round([double]$row."Simulation Time (s)", 2)
        $directHash[$time] = $row."Credits"
    }

    foreach ($row in $sessionData) {
        $time = [math]::Round([double]$row."Simulation Time (s)", 2)
        $sessionHash[$time] = $row."Credits"
    }

    foreach ($row in $transactionData) {
        $time = [math]::Round([double]$row."Simulation Time (s)", 2)
        $transactionHash[$time] = $row."Credits"
    }

    # Merge data
    $mergedCredits = @()
    $mergedCredits += "Simulation Time (s),Direct,Session,Transaction"

    $allTimes = $directHash.Keys | Sort-Object
    foreach ($time in $allTimes) {
        if ($sessionHash.ContainsKey($time) -and $transactionHash.ContainsKey($time)) {
            $mergedCredits += "$time,$($directHash[$time]),$($sessionHash[$time]),$($transactionHash[$time])"
        }
    }

    $mergedCredits | Out-File "./output/pooling_comparison/credits_combined.csv" -Encoding UTF8
    Write-Host "✓ Created credits_combined.csv" -ForegroundColor Green
}

# Generate individual graphs for PowerPoint overlay (with distinct colors!)
if (-not (Get-Command graph -ErrorAction SilentlyContinue)) {
    Write-Host "graph-cli not found. Install with: pip install graph-cli" -ForegroundColor Yellow
    Write-Host "Skipping graph generation" -ForegroundColor Yellow
    Write-Host "(Merged CSV files are still available for manual plotting)" -ForegroundColor Yellow
} else {
    Write-Host "Generating individual charts (with distinct colors for overlay)..." -ForegroundColor Blue

    # Generate individual latency and credits graphs with distinct colors
    foreach ($modeInfo in $modes) {
        $mode = $modeInfo.Name
        $modeDir = "./output/pooling_comparison/$mode"

        # Set color based on mode
        $color = switch ($mode) {
            "direct" { "red" }       # Red = worst (highest overhead)
            "session" { "green" }    # Green = best (no overhead)
            "transaction" { "orange" } # Orange = middle (8ms overhead)
        }

        # Generate latency graph
        $latencyFile = "$modeDir/simulation_latency.csv"
        if (Test-Path $latencyFile) {
            $latency = Import-Csv $latencyFile
            $latencyMax = ($latency | ForEach-Object { $_."Latency (ms)" } | Measure-Object -Maximum).Maximum
            if ($null -eq $latencyMax) { $latencyMax = 1 }

            graph $latencyFile --title "Latency - $mode" --color $color --yrange="0:$latencyMax" -o "$modeDir/latency.png"
            Write-Host "✓ Generated $mode latency graph ($color)" -ForegroundColor Green
        }

        # Generate credits graph
        $creditsFile = "$modeDir/simulation_credits.csv"
        if (Test-Path $creditsFile) {
            $credits = Import-Csv $creditsFile
            $creditMax = ($credits | Measure-Object -Property "Credits" -Maximum).Maximum
            if ($null -eq $creditMax) { $creditMax = 1 }

            graph $creditsFile --title "Credits - $mode" --color $color --yrange="0:$creditMax" -o "$modeDir/credits.png"
            Write-Host "✓ Generated $mode credits graph ($color)" -ForegroundColor Green
        }
    }

    # Also generate combined overlay graph (all colors in one)
    $latencyCombined = "./output/pooling_comparison/latency_combined.csv"
    if (Test-Path $latencyCombined) {
        $combinedData = Import-Csv $latencyCombined
        $latencyMax = ($combinedData | ForEach-Object {
            [math]::Max([math]::Max([double]$_."Direct (ms)", [double]$_."Session (ms)"), [double]$_."Transaction (ms)")
        } | Measure-Object -Maximum).Maximum

        if ($null -eq $latencyMax) { $latencyMax = 1 }

        graph $latencyCombined --title "Connection Pooling Latency Comparison" --yrange="0:$latencyMax" -o "./output/pooling_comparison/latency_comparison.png"
        Write-Host "✓ Generated latency_comparison.png (all modes)" -ForegroundColor Green
    }

    $creditsCombined = "./output/pooling_comparison/credits_combined.csv"
    if (Test-Path $creditsCombined) {
        $combinedData = Import-Csv $creditsCombined
        $creditMax = ($combinedData | ForEach-Object {
            [math]::Max([math]::Max([double]$_.Direct, [double]$_.Session), [double]$_.Transaction)
        } | Measure-Object -Maximum).Maximum

        if ($null -eq $creditMax) { $creditMax = 1 }

        graph $creditsCombined --title "Connection Pooling CPU Credits Comparison" --yrange="0:$creditMax" -o "./output/pooling_comparison/credits_comparison.png"
        Write-Host "✓ Generated credits_comparison.png (all modes)" -ForegroundColor Green
    }

    # Generate summary bar charts
    Write-Host "Generating summary bar charts..." -ForegroundColor Blue

    # Calculate average latencies
    $directData = Import-Csv "./output/pooling_comparison/direct/simulation_latency.csv"
    $sessionData = Import-Csv "./output/pooling_comparison/session/simulation_latency.csv"
    $transactionData = Import-Csv "./output/pooling_comparison/transaction/simulation_latency.csv"

    $directAvg = ($directData | ForEach-Object { [double]$_."Latency (ms)" } | Measure-Object -Average).Average
    $sessionAvg = ($sessionData | ForEach-Object { [double]$_."Latency (ms)" } | Measure-Object -Average).Average
    $transactionAvg = ($transactionData | ForEach-Object { [double]$_."Latency (ms)" } | Measure-Object -Average).Average

    # Create summary CSV for bar chart
    $summaryCsv = @"
Mode,Average Latency (ms)
Direct,$([math]::Round($directAvg, 2))
Session,$([math]::Round($sessionAvg, 2))
Transaction,$([math]::Round($transactionAvg, 2))
"@
    $summaryCsv | Out-File "./output/pooling_comparison/latency_summary.csv" -Encoding UTF8

    # Generate bar chart (full scale from 0)
    graph "./output/pooling_comparison/latency_summary.csv" --bar --bar-label --title "Average Latency Comparison" --ylabel "Latency (ms)" -o "./output/pooling_comparison/latency_bar_chart.png"
    Write-Host "✓ Generated latency_bar_chart.png (full scale)" -ForegroundColor Green

    # Generate zoomed bar chart (emphasizes differences)
    $summaryData = Import-Csv "./output/pooling_comparison/latency_summary.csv"
    $latencies = $summaryData | ForEach-Object { [double]$_."Average Latency (ms)" }
    $minLatency = ($latencies | Measure-Object -Minimum).Minimum
    $maxLatency = ($latencies | Measure-Object -Maximum).Maximum
    $rangeMin = [math]::Floor($minLatency - 0.1)
    $rangeMax = [math]::Ceiling($maxLatency + 0.1)

    graph "./output/pooling_comparison/latency_summary.csv" --bar --bar-label --title "Average Latency Comparison (Zoomed)" --ylabel "Latency (ms)" --yrange="$rangeMin`:$rangeMax" -o "./output/pooling_comparison/latency_bar_chart_zoomed.png"
    Write-Host "✓ Generated latency_bar_chart_zoomed.png (emphasizes differences)" -ForegroundColor Green
}

# Calculate and display summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

$summaryFormat = "{0,-30} {1,15} {2,15} {3,15}"
Write-Host ($summaryFormat -f "Mode", "Avg Latency", "Min Latency", "Max Latency")
Write-Host "───────────────────────────────────────────────────────────────────────────"

foreach ($modeInfo in $modes) {
    $mode = $modeInfo.Name
    $csvFile = "./output/pooling_comparison/$mode/simulation_latency.csv"

    if (Test-Path $csvFile) {
        $data = Import-Csv $csvFile
        $latencies = $data | ForEach-Object { [double]$_."Latency (ms)" }

        $avg = ($latencies | Measure-Object -Average).Average
        $min = ($latencies | Measure-Object -Minimum).Minimum
        $max = ($latencies | Measure-Object -Maximum).Maximum

        $modeLabel = switch ($mode) {
            "direct" { "Direct (50ms overhead)" }
            "session" { "Session Pooling (no overhead)" }
            "transaction" { "Transaction (8ms overhead)" }
        }

        Write-Host ($summaryFormat -f $modeLabel, "$([math]::Round($avg, 2)) ms", "$([math]::Round($min, 2)) ms", "$([math]::Round($max, 2)) ms")
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
