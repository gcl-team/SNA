#!/bin/bash

# Exit on error, undefined variables, and pipe failures
set -euo pipefail

# Resolve script directory and cd into it to ensure relative paths work
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# SYNOPSIS
#     Run PostgreSQL connection pooling comparison across all three modes.
# DESCRIPTION
#     This script runs the azure-pgsql-pooling simulation for Direct, Session, and Transaction modes,
#     backs up the CSV results, and generates comparison graphs using graph-cli.
# EXAMPLES
#     # Run comparison with default settings (120 seconds, pool size 20)
#     ./pooling-comparison.sh
#
#     # Run with custom duration and pool size
#     ./pooling-comparison.sh --duration 300 --pool-size 30

# ANSI color codes
CYAN='\033[0;36m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default configuration
DURATION=120
POOL_SIZE=20
SERIES="B"
SIZE="2ms"
INITIAL_CREDITS=60

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --duration)
            DURATION="$2"
            shift 2
            ;;
        --pool-size)
            POOL_SIZE="$2"
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
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║     PostgreSQL Connection Pooling Comparison                  ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${BLUE}Configuration:${NC}"
echo -e "  • Instance: Azure ${SERIES}.${SIZE}"
echo -e "  • Duration: ${DURATION} seconds"
echo -e "  • Pool Size: ${POOL_SIZE}"
echo -e "  • Initial Credits: ${INITIAL_CREDITS}"
echo ""

MODES=("direct" "session" "transaction")
OVERALL_START=$SECONDS

# Create backup directory
mkdir -p ./output/pooling_comparison

# Function to run a single mode
run_mode() {
    local MODE=$1
    local MODE_LABEL=$2

    echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"
    echo -e "${CYAN}Running: ${MODE_LABEL}${NC}"
    echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"

    MODE_START=$SECONDS

    # Run simulation - use if ! pattern to handle errors properly with set -e
    if ! dotnet run --project ../SimNextgenApp.Demo.csproj -- demo azure-pgsql-pooling \
        --mode "$MODE" \
        --pool-size "$POOL_SIZE" \
        --series "$SERIES" \
        --size "$SIZE" \
        --duration "$DURATION" \
        --initial-credits "$INITIAL_CREDITS"; then
        echo -e "${RED}Simulation failed for ${MODE}${NC}"
        return 1
    fi

    # Backup results
    mkdir -p "./output/pooling_comparison/${MODE}"
    if [ -f ./output/simulation_latency.csv ]; then
        cp ./output/simulation_latency.csv "./output/pooling_comparison/${MODE}/simulation_latency.csv"
        echo -e "${GREEN}✓ Backed up latency data${NC}"
    fi
    if [ -f ./output/simulation_credits.csv ]; then
        cp ./output/simulation_credits.csv "./output/pooling_comparison/${MODE}/simulation_credits.csv"
        echo -e "${GREEN}✓ Backed up credits data${NC}"
    fi

    MODE_ELAPSED=$(($SECONDS - $MODE_START))
    echo -e "${GREEN}✓ ${MODE_LABEL} completed (${MODE_ELAPSED}s)${NC}"
    echo ""
}

# Run all three modes
run_mode "direct" "Direct Connections (50ms overhead)"
run_mode "session" "Session Pooling (no overhead)"
run_mode "transaction" "Transaction Pooling (8ms overhead)"

# Merge CSVs and generate comparison graphs
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}Merging Results & Generating Comparison Graphs${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"

# Merge latency CSVs
LATENCY_DIRECT="./output/pooling_comparison/direct/simulation_latency.csv"
LATENCY_SESSION="./output/pooling_comparison/session/simulation_latency.csv"
LATENCY_TRANSACTION="./output/pooling_comparison/transaction/simulation_latency.csv"

if [ -f "$LATENCY_DIRECT" ] && [ -f "$LATENCY_SESSION" ] && [ -f "$LATENCY_TRANSACTION" ]; then
    echo -e "${BLUE}Merging latency data...${NC}"

    # Create merged latency CSV using a three-pass approach
    {
        echo "Simulation Time (s),Direct (ms),Session (ms),Transaction (ms)"

        # Build associative arrays using join
        join -t, -j1 -o 1.1,1.2,2.2 \
            <(tail -n +2 "$LATENCY_DIRECT" | awk -F, '{printf "%.2f,%s\n", $1, $2}' | sort -t, -k1 -n) \
            <(tail -n +2 "$LATENCY_SESSION" | awk -F, '{printf "%.2f,%s\n", $1, $2}' | sort -t, -k1 -n) \
        | join -t, -j1 -o 1.1,1.2,1.3,2.2 - \
            <(tail -n +2 "$LATENCY_TRANSACTION" | awk -F, '{printf "%.2f,%s\n", $1, $2}' | sort -t, -k1 -n)
    } > "./output/pooling_comparison/latency_combined.csv"

    echo -e "${GREEN}✓ Created latency_combined.csv${NC}"
fi

# Merge credits CSVs
CREDITS_DIRECT="./output/pooling_comparison/direct/simulation_credits.csv"
CREDITS_SESSION="./output/pooling_comparison/session/simulation_credits.csv"
CREDITS_TRANSACTION="./output/pooling_comparison/transaction/simulation_credits.csv"

if [ -f "$CREDITS_DIRECT" ] && [ -f "$CREDITS_SESSION" ] && [ -f "$CREDITS_TRANSACTION" ]; then
    echo -e "${BLUE}Merging credits data...${NC}"

    # Create merged credits CSV using a three-pass approach
    {
        echo "Simulation Time (s),Direct,Session,Transaction"

        # Build associative arrays using join
        join -t, -j1 -o 1.1,1.2,2.2 \
            <(tail -n +2 "$CREDITS_DIRECT" | awk -F, '{printf "%.2f,%s\n", $1, $2}' | sort -t, -k1 -n) \
            <(tail -n +2 "$CREDITS_SESSION" | awk -F, '{printf "%.2f,%s\n", $1, $2}' | sort -t, -k1 -n) \
        | join -t, -j1 -o 1.1,1.2,1.3,2.2 - \
            <(tail -n +2 "$CREDITS_TRANSACTION" | awk -F, '{printf "%.2f,%s\n", $1, $2}' | sort -t, -k1 -n)
    } > "./output/pooling_comparison/credits_combined.csv"

    echo -e "${GREEN}✓ Created credits_combined.csv${NC}"
fi

# Generate individual graphs for PowerPoint overlay (with distinct colors!)
if ! command -v graph &> /dev/null; then
    echo -e "${YELLOW}graph-cli not found. Install with: pip install graph-cli${NC}"
    echo -e "${YELLOW}Skipping graph generation${NC}"
    echo -e "${YELLOW}(Merged CSV files are still available for manual plotting)${NC}"
else
    echo -e "${BLUE}Generating individual charts (with distinct colors for overlay)...${NC}"

    # Generate individual latency graphs with distinct colors
    for MODE in "${MODES[@]}"; do
        MODE_DIR="./output/pooling_comparison/${MODE}"

        # Set color based on mode
        case $MODE in
            direct) COLOR="red" ;;       # Red = worst (highest overhead)
            session) COLOR="green" ;;    # Green = best (no overhead)
            transaction) COLOR="orange" ;; # Orange = middle (8ms overhead)
        esac

        if [ -f "${MODE_DIR}/simulation_latency.csv" ]; then
            LATENCY_MAX=$(awk -F, 'BEGIN {max=0} NR>1 {if($2>max) max=$2} END {print (max==0?1:max)}' "${MODE_DIR}/simulation_latency.csv")
            graph "${MODE_DIR}/simulation_latency.csv" \
                --title "Latency - ${MODE}" \
                --color "$COLOR" \
                --yrange=0:$LATENCY_MAX \
                -o "${MODE_DIR}/latency.png"
            echo -e "${GREEN}✓ Generated ${MODE} latency graph (${COLOR})${NC}"
        fi

        if [ -f "${MODE_DIR}/simulation_credits.csv" ]; then
            CREDIT_MAX=$(awk -F, 'BEGIN {max=0} NR>1 {if($2>max) max=$2} END {print (max==0?1:max)}' "${MODE_DIR}/simulation_credits.csv")
            graph "${MODE_DIR}/simulation_credits.csv" \
                --title "Credits - ${MODE}" \
                --color "$COLOR" \
                --yrange=0:$CREDIT_MAX \
                -o "${MODE_DIR}/credits.png"
            echo -e "${GREEN}✓ Generated ${MODE} credits graph (${COLOR})${NC}"
        fi
    done

    # Also generate combined overlay graph (all colors in one)
    if [ -f "./output/pooling_comparison/latency_combined.csv" ]; then
        LATENCY_MAX=$(awk -F, 'BEGIN {max=0} NR>1 {for(i=2;i<=NF;i++){if($i>max)max=$i}} END {print (max==0?1:max)}' "./output/pooling_comparison/latency_combined.csv")
        graph "./output/pooling_comparison/latency_combined.csv" \
            --title "Connection Pooling Latency Comparison" \
            --yrange=0:$LATENCY_MAX \
            -o "./output/pooling_comparison/latency_comparison.png"
        echo -e "${GREEN}✓ Generated latency_comparison.png (all modes)${NC}"
    fi

    # Generate summary bar charts
    echo -e "${BLUE}Generating summary bar charts...${NC}"

    # Calculate average latencies
    DIRECT_AVG=$(awk -F, 'NR>1 {sum+=$2; count++} END {printf "%.2f", sum/count}' "./output/pooling_comparison/direct/simulation_latency.csv")
    SESSION_AVG=$(awk -F, 'NR>1 {sum+=$2; count++} END {printf "%.2f", sum/count}' "./output/pooling_comparison/session/simulation_latency.csv")
    TRANSACTION_AVG=$(awk -F, 'NR>1 {sum+=$2; count++} END {printf "%.2f", sum/count}' "./output/pooling_comparison/transaction/simulation_latency.csv")

    # Create summary CSV for bar chart
    cat > "./output/pooling_comparison/latency_summary.csv" << EOF
Mode,Average Latency (ms)
Direct,$DIRECT_AVG
Session,$SESSION_AVG
Transaction,$TRANSACTION_AVG
EOF

    # Generate bar chart (full scale from 0)
    graph "./output/pooling_comparison/latency_summary.csv" \
        --bar \
        --bar-label \
        --title "Average Latency Comparison" \
        --ylabel "Latency (ms)" \
        -o "./output/pooling_comparison/latency_bar_chart.png"
    echo -e "${GREEN}✓ Generated latency_bar_chart.png (full scale)${NC}"

    # Generate zoomed bar chart (emphasizes differences)
    MIN_LATENCY=$(awk -F, 'NR>1 {if(NR==2 || $2<min) min=$2} END {printf "%.0f", min}' "./output/pooling_comparison/latency_summary.csv")
    MAX_LATENCY=$(awk -F, 'NR>1 {if(NR==2 || $2>max) max=$2} END {printf "%.0f", max}' "./output/pooling_comparison/latency_summary.csv")
    RANGE_MIN=$(awk "BEGIN {printf \"%.1f\", $MIN_LATENCY - 0.1}")
    RANGE_MAX=$(awk "BEGIN {printf \"%.1f\", $MAX_LATENCY + 0.1}")

    graph "./output/pooling_comparison/latency_summary.csv" \
        --bar \
        --bar-label \
        --title "Average Latency Comparison (Zoomed)" \
        --ylabel "Latency (ms)" \
        --yrange=$RANGE_MIN:$RANGE_MAX \
        -o "./output/pooling_comparison/latency_bar_chart_zoomed.png"
    echo -e "${GREEN}✓ Generated latency_bar_chart_zoomed.png (emphasizes differences)${NC}"

    if [ -f "./output/pooling_comparison/credits_combined.csv" ]; then
        CREDIT_MAX=$(awk -F, 'BEGIN {max=0} NR>1 {for(i=2;i<=NF;i++){if($i>max)max=$i}} END {print (max==0?1:max)}' "./output/pooling_comparison/credits_combined.csv")
        graph "./output/pooling_comparison/credits_combined.csv" \
            --title "Connection Pooling CPU Credits Comparison" \
            --yrange=0:$CREDIT_MAX \
            -o "./output/pooling_comparison/credits_comparison.png"
        echo -e "${GREEN}✓ Generated credits_comparison.png (all modes)${NC}"
    fi
fi

# Calculate and display summary
echo ""
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}Summary${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════════════${NC}"

printf "%-30s %-15s %-15s %-15s\n" "Mode" "Avg Latency" "Min Latency" "Max Latency"
echo "───────────────────────────────────────────────────────────────────────────"

for MODE in "${MODES[@]}"; do
    CSV_FILE="./output/pooling_comparison/${MODE}/simulation_latency.csv"
    if [ -f "$CSV_FILE" ]; then
        # Calculate stats using awk
        STATS=$(awk -F, 'NR>1 {
            sum+=$2;
            count++;
            if(NR==2 || $2<min) min=$2;
            if(NR==2 || $2>max) max=$2
        }
        END {
            printf "%.2f %.2f %.2f", sum/count, min, max
        }' "$CSV_FILE")

        read AVG MIN MAX <<< "$STATS"

        MODE_LABEL=""
        case $MODE in
            direct) MODE_LABEL="Direct (50ms overhead)" ;;
            session) MODE_LABEL="Session Pooling (no overhead)" ;;
            transaction) MODE_LABEL="Transaction (8ms overhead)" ;;
        esac

        printf "%-30s %10.2f ms   %10.2f ms   %10.2f ms\n" "$MODE_LABEL" "$AVG" "$MIN" "$MAX"
    fi
done



OVERALL_ELAPSED=$(($SECONDS - $OVERALL_START))
echo -e "${GREEN}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║           Comparison Complete! (Total Duration: ${OVERALL_ELAPSED}s)          ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "Results saved in: ${BLUE}./output/pooling_comparison/${NC}"
