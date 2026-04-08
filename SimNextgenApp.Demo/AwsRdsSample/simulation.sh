#!/bin/bash

# SYNOPSIS
#     Run the SNA simulation and optionally plot results.
# DESCRIPTION
#     This script builds the SimNextgenApp.Demo project and runs the AWS RDS simulation with the provided arguments.
#     Supported instance types include t3, t4g, and m5 families with various sizes.
#     After the simulation completes, it generates graphs for credits and latency if the graph-cli tool is installed.
# GRAFANA CLOUD INTEGRATION
#     Use --grafana true to export metrics to Grafana Cloud via OpenTelemetry.
#     Requires GRAFANA_API_KEY environment variable (format: INSTANCE_ID:API_TOKEN).
#     Set the API key before running the script and use the example below as a reference for configuration.
# EXAMPLES
#     # Basic simulation with CSV export
#     ./simulation.sh aws-rds-burst --family t3 --size medium --duration 720 --initial-credits 10
#
#     # With Grafana Cloud export
#     export GRAFANA_API_KEY="123456:glc_abc123..."
#     ./simulation.sh aws-rds-burst --family t3 --size medium --duration 720 --grafana true

# ANSI color codes
CYAN='\033[0;36m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
NC='\033[0m' # No Color

echo -e "${CYAN}Building project and starting SNA simulation...${NC}"

START_TIME=$SECONDS

# Run the simulation, passing all script arguments through
dotnet run --project ../SimNextgenApp.Demo.csproj -- demo "$@"

if [ $? -ne 0 ]; then
    echo -e "${RED}Simulation failed${NC}"
    exit 1
fi

echo -e "${GREEN}Generating graphs...${NC}"

# Check if graph-cli is installed
if ! command -v graph &> /dev/null
then
    echo -e "${YELLOW}graph-cli not found. Please run: pip install graph-cli${NC}"
else
    # Generate credits graph only if credits CSV exists (burstable instances only)
    if [ -f ./output/simulation_credits.csv ]; then
        # Calculate Max for scaling (check both Credits and Surplus Credit Debt columns)
        # Initialize max=0 to handle empty CSV edge case
        CREDIT_MAX=$(awk -F, 'BEGIN {max=0} NR>1 {if($2>max) max=$2; if($3>max) max=$3} END {print (max==0?1:max)}' ./output/simulation_credits.csv)
        graph ./output/simulation_credits.csv --title "AWS RDS Credits (Regular vs Surplus)" --yrange=0:$CREDIT_MAX -o ./output/simulation_credits.png
        echo -e "${GREEN}Credits graph generated${NC}"
    else
        echo -e "${YELLOW}Skipping credits graph (not a burstable instance)${NC}"
    fi

    # Generate latency graph if CSV exists
    if [ -f ./output/simulation_latency.csv ]; then
        LATENCY_MAX=$(awk -F, 'BEGIN {max=0} NR>1 {if($2>max) max=$2} END {print (max==0?1:max)}' ./output/simulation_latency.csv)
        graph ./output/simulation_latency.csv --title "Simulation Latency" --yrange=0:$LATENCY_MAX -o ./output/simulation_latency.png
        echo -e "${GREEN}Latency graph generated${NC}"
    else
        echo -e "${YELLOW}Latency CSV not found${NC}"
    fi
fi

ELAPSED_TIME=$(($SECONDS - $START_TIME))
echo -e "${GREEN}Simulation completed successfully! (Duration: ${ELAPSED_TIME}s)${NC}"
