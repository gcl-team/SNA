#!/bin/bash

# Exit on error, undefined variables, and pipe failures
set -euo pipefail

# Resolve script directory and cd into it to ensure relative paths work
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# SYNOPSIS
#     Compare PostgreSQL connection pool performance across different pool sizes.
# DESCRIPTION
#     This script runs the azure-pgsql-pooling simulation in Session pooling mode
#     with varying pool sizes to determine optimal configuration.
# EXAMPLES
#     # Run comparison with default settings (180 seconds per size)
#     ./pool-size-comparison.sh
#
#     # Run with custom duration
#     ./pool-size-comparison.sh --duration 300

# ANSI color codes
CYAN='\033[0;36m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default configuration
DURATION=180
SERIES="B"
SIZE="2ms"
INITIAL_CREDITS=60
POOL_SIZES=(5 10 20 50 100)

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --duration)
            DURATION="$2"
            shift 2
            ;;
        --series)
            SERIES="$2"
            shift 2
            ;;
        --size)
            SIZE="$2"
            shift 2
            ;;
        --initial-credits)
            INITIAL_CREDITS="$2"
            shift 2
            ;;
        --pool-sizes)
            IFS=',' read -ra POOL_SIZES <<< "$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║           PostgreSQL Pool Size Optimization Analysis           ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${BLUE}Configuration:${NC}"
echo -e "  • Instance: Azure ${SERIES}.${SIZE}"
echo -e "  • Duration per size: ${DURATION} seconds"
echo -e "  • Pool sizes: ${POOL_SIZES[*]}"
echo -e "  • Initial Credits: ${INITIAL_CREDITS}"
echo -e "  • Pooling Mode: Session (optimal for comparison)"
echo ""

OVERALL_START=$SECONDS

# Create output directory
mkdir -p ./output/pool_size_comparison

# Function to run a single pool size
run_pool_size() {
    local POOL_SIZE=$1

    echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"
    echo -e "${CYAN}Testing Pool Size: ${POOL_SIZE}${NC}"
    echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"

    POOL_START=$SECONDS

    # Run simulation with session pooling - use if ! pattern to handle errors properly with set -e
    if ! dotnet run --project ../SimNextgenApp.Demo.csproj -- demo azure-pgsql-pooling \
        --mode session \
        --pool-size "$POOL_SIZE" \
        --series "$SERIES" \
        --size "$SIZE" \
        --duration "$DURATION" \
        --initial-credits "$INITIAL_CREDITS"; then
        echo -e "${RED}Simulation failed for pool size ${POOL_SIZE}${NC}"
        return 1
    fi

    # Backup results
    mkdir -p "./output/pool_size_comparison/pool_${POOL_SIZE}"
    if [ -f ./output/simulation_latency.csv ]; then
        cp ./output/simulation_latency.csv "./output/pool_size_comparison/pool_${POOL_SIZE}/simulation_latency.csv"
        echo -e "${GREEN}✓ Backed up latency data${NC}"
    fi
    if [ -f ./output/simulation_credits.csv ]; then
        cp ./output/simulation_credits.csv "./output/pool_size_comparison/pool_${POOL_SIZE}/simulation_credits.csv"
        echo -e "${GREEN}✓ Backed up credits data${NC}"
    fi

    POOL_ELAPSED=$(($SECONDS - $POOL_START))
    echo -e "${GREEN}✓ Pool size ${POOL_SIZE} completed (${POOL_ELAPSED}s)${NC}"
    echo ""
}

# Run all pool sizes
for POOL_SIZE in "${POOL_SIZES[@]}"; do
    run_pool_size "$POOL_SIZE"
done

# Generate summary statistics
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}Generating Summary Statistics${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"

# Create summary CSV for latency vs pool size
cat > "./output/pool_size_comparison/latency_vs_pool_size.csv" << 'EOF'
Pool Size,Median Latency (ms),Min Latency (ms),Max Latency (ms)
EOF

for POOL_SIZE in "${POOL_SIZES[@]}"; do
    CSV_FILE="./output/pool_size_comparison/pool_${POOL_SIZE}/simulation_latency.csv"
    if [ -f "$CSV_FILE" ]; then
        # Calculate median and min/max using sort for portability
        MIN_MAX=$(awk -F, 'NR>1 {
            if(NR==2 || $2<min) min=$2;
            if(NR==2 || $2>max) max=$2;
        }
        END {
            printf "%.2f,%.2f", min, max;
        }' "$CSV_FILE")

        MEDIAN=$(awk -F, 'NR>1 {print $2}' "$CSV_FILE" | sort -n | awk '{a[NR]=$1} END {n=NR; mid=int(n/2); if(n%2==1) printf "%.2f", a[mid+1]; else printf "%.2f", (a[mid]+a[mid+1])/2}')

        echo "${POOL_SIZE},${MEDIAN},${MIN_MAX}" >> "./output/pool_size_comparison/latency_vs_pool_size.csv"
    fi
done

echo -e "${GREEN}✓ Created latency_vs_pool_size.csv${NC}"

# Create simplified summary for bar charts
cat > "./output/pool_size_comparison/latency_summary.csv" << 'EOF'
Pool Size,Median Latency (ms)
EOF

for POOL_SIZE in "${POOL_SIZES[@]}"; do
    CSV_FILE="./output/pool_size_comparison/pool_${POOL_SIZE}/simulation_latency.csv"
    if [ -f "$CSV_FILE" ]; then
        MEDIAN=$(awk -F, 'NR>1 {print $2}' "$CSV_FILE" | sort -n | awk '{a[NR]=$1} END {n=NR; mid=int(n/2); if(n%2==1) printf "%.2f", a[mid+1]; else printf "%.2f", (a[mid]+a[mid+1])/2}')
        echo "${POOL_SIZE},${MEDIAN}" >> "./output/pool_size_comparison/latency_summary.csv"
    fi
done

echo -e "${GREEN}✓ Created latency_summary.csv${NC}"

# Generate graphs if graph-cli is available
if ! command -v graph &> /dev/null; then
    echo -e "${YELLOW}graph-cli not found. Install with: pip install graph-cli${NC}"
    echo -e "${YELLOW}Skipping graph generation${NC}"
    echo -e "${YELLOW}(CSV files are still available for manual plotting)${NC}"
else
    echo -e "${BLUE}Generating comparison charts...${NC}"

    # Generate line chart: Median Latency vs Pool Size
    graph "./output/pool_size_comparison/latency_summary.csv" \
        --title "Median Latency vs Pool Size (Session Pooling)" \
        --xlabel "Pool Size" \
        --ylabel "Latency (ms)" \
        -o "./output/pool_size_comparison/latency_vs_pool_size.png"
    echo -e "${GREEN}✓ Generated latency_vs_pool_size.png (line chart)${NC}"

    # Generate bar chart for easier reading (full scale from 0)
    graph "./output/pool_size_comparison/latency_summary.csv" \
        --title "Median Latency vs Pool Size (Session Pooling)" \
        --xlabel "Pool Size" \
        --ylabel "Latency (ms)" \
        --bar \
        -o "./output/pool_size_comparison/latency_bar_chart.png"
    echo -e "${GREEN}✓ Generated latency_bar_chart.png (full scale)${NC}"

    # Generate zoomed bar chart (emphasizes differences)
    MIN_LATENCY=$(awk -F, 'NR>1 {if(NR==2 || $2<min) min=$2} END {printf "%.0f", min}' "./output/pool_size_comparison/latency_summary.csv")
    MAX_LATENCY=$(awk -F, 'NR>1 {if(NR==2 || $2>max) max=$2} END {printf "%.0f", max}' "./output/pool_size_comparison/latency_summary.csv")
    RANGE_MIN=$(awk "BEGIN {printf \"%.0f\", $MIN_LATENCY - 5}")
    RANGE_MAX=$(awk "BEGIN {printf \"%.0f\", $MAX_LATENCY + 5}")

    graph "./output/pool_size_comparison/latency_summary.csv" \
        --bar \
        --title "Median Latency vs Pool Size (Zoomed)" \
        --xlabel "Pool Size" \
        --ylabel "Latency (ms)" \
        --yrange=$RANGE_MIN:$RANGE_MAX \
        -o "./output/pool_size_comparison/latency_bar_chart_zoomed.png"
    echo -e "${GREEN}✓ Generated latency_bar_chart_zoomed.png (emphasizes differences)${NC}"

    # Generate individual time series for each pool size
    for POOL_SIZE in "${POOL_SIZES[@]}"; do
        CSV_FILE="./output/pool_size_comparison/pool_${POOL_SIZE}/simulation_latency.csv"
        if [ -f "$CSV_FILE" ]; then
            LATENCY_MAX=$(awk -F, 'BEGIN {max=0} NR>1 {if($2>max) max=$2} END {print (max==0?1:max)}' "$CSV_FILE")
            graph "$CSV_FILE" \
                --title "Latency Time Series - Pool Size ${POOL_SIZE}" \
                --color "blue" \
                --yrange=0:$LATENCY_MAX \
                -o "./output/pool_size_comparison/pool_${POOL_SIZE}/latency_timeseries.png"
        fi
    done
    echo -e "${GREEN}✓ Generated individual time series charts${NC}"
fi

# Display summary table
echo ""
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}Summary: Latency vs Pool Size${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"

printf "%-12s %-15s %-15s %-15s\n" "Pool Size" "Median (P50)" "Min Latency" "Max Latency"
echo "────────────────────────────────────────────────────────────────────"

for POOL_SIZE in "${POOL_SIZES[@]}"; do
    CSV_FILE="./output/pool_size_comparison/pool_${POOL_SIZE}/simulation_latency.csv"
    if [ -f "$CSV_FILE" ]; then
        # Calculate min/max
        MIN_MAX=$(awk -F, 'NR>1 {
            if(NR==2 || $2<min) min=$2;
            if(NR==2 || $2>max) max=$2
        }
        END {
            printf "%.2f %.2f", min, max
        }' "$CSV_FILE")

        # Calculate median using sort for portability
        MEDIAN=$(awk -F, 'NR>1 {print $2}' "$CSV_FILE" | sort -n | awk '{a[NR]=$1} END {n=NR; mid=int(n/2); if(n%2==1) printf "%.2f", a[mid+1]; else printf "%.2f", (a[mid]+a[mid+1])/2}')

        read MIN MAX <<< "$MIN_MAX"
        printf "%-12s %10.2f ms   %10.2f ms   %10.2f ms\n" "$POOL_SIZE" "$MEDIAN" "$MIN" "$MAX"
    fi
done

echo ""
echo -e "${BLUE}Recommendations:${NC}"

# Find minimum latency
MIN_LATENCY=$(awk -F, 'NR>1 {if(NR==2 || $2<min) min=$2} END {printf "%.2f", min}' "./output/pool_size_comparison/latency_summary.csv")

# Find smallest pool size that achieves near-optimal performance (within 1% of minimum)
OPTIMAL_SIZE=$(awk -F, -v minlat="$MIN_LATENCY" 'NR>1 {
    threshold = minlat * 1.01;
    if ($2 <= threshold) {
        if (optimal == "" || $1 < optimal) {
            optimal = $1;
            optlat = $2;
        }
    }
} END {
    printf "%s", optimal
}' "./output/pool_size_comparison/latency_summary.csv")

OPTIMAL_LATENCY=$(awk -F, -v size="$OPTIMAL_SIZE" 'NR>1 && $1==size {printf "%.2f", $2}' "./output/pool_size_comparison/latency_summary.csv")

echo -e "  • ${GREEN}Optimal pool size: ${OPTIMAL_SIZE} (median latency: ${OPTIMAL_LATENCY}ms)${NC}"
echo -e "  • ${CYAN}Note: Smallest pool size achieving near-optimal performance (≤1% of minimum)${NC}"

# Check for diminishing returns (when improvement < 5%)
# Sort pool sizes numerically for meaningful comparison
IFS=$'\n' SORTED_POOL_SIZES=($(printf '%s\n' "${POOL_SIZES[@]}" | sort -n))
unset IFS

LAST_AVG=""
for POOL_SIZE in "${SORTED_POOL_SIZES[@]}"; do
    # Skip the optimal pool size (already highlighted above)
    if [ "$POOL_SIZE" -eq "$OPTIMAL_SIZE" ]; then
        CURRENT_AVG=$(awk -F, -v size="$POOL_SIZE" 'NR>1 && $1==size {print $2}' "./output/pool_size_comparison/latency_summary.csv")
        LAST_AVG=$CURRENT_AVG
        continue
    fi

    CURRENT_AVG=$(awk -F, -v size="$POOL_SIZE" 'NR>1 && $1==size {print $2}' "./output/pool_size_comparison/latency_summary.csv")
    if [ -n "$LAST_AVG" ]; then
        # Calculate improvement percentage using awk (no bc dependency)
        IMPROVEMENT=$(awk "BEGIN {printf \"%.2f\", ($LAST_AVG - $CURRENT_AVG) / $LAST_AVG * 100}")
        IS_NEGATIVE=$(awk "BEGIN {print ($IMPROVEMENT < 0) ? 1 : 0}")

        if [ "$IS_NEGATIVE" -eq 1 ]; then
            echo -e "  • ${YELLOW}Pool size ${POOL_SIZE}: Performance degraded (${IMPROVEMENT}% worse)${NC}"
        else
            IS_SMALL=$(awk "BEGIN {print ($IMPROVEMENT < 5) ? 1 : 0}")
            if [ "$IS_SMALL" -eq 1 ]; then
                echo -e "  • ${YELLOW}Pool size ${POOL_SIZE}: Diminishing returns (<5% improvement)${NC}"
            fi
        fi
    fi
    LAST_AVG=$CURRENT_AVG
done

OVERALL_ELAPSED=$(($SECONDS - $OVERALL_START))
echo ""
echo -e "${GREEN}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║            Pool Size Comparison Complete! (Total: ${OVERALL_ELAPSED}s)         ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "Results saved in: ${BLUE}./output/pool_size_comparison/${NC}"
