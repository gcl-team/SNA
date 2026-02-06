#!/bin/bash

# SYNOPSIS
#     Run the SNA simulation and optionally plot results.
# DESCRIPTION
#     This script builds the SimNextgenApp.Demo project and runs the AWS RDS simulation with the provided arguments.
#     Supported instance types include t3.medium, t4g.medium, and m5.large.
#     After the simulation completes, it generates graphs for credits and latency if the graph-cli tool is installed.
# EXAMPLE
#     ./simulation.sh aws-rds-burst --instance-type t3.medium --duration 720 --initial-credits 10 --unlimited-credits false

# ANSI color codes
CYAN='\033[0;36m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
NC='\033[0m' # No Color

echo -e "${CYAN}Building project and starting SNA simulation...${NC}"

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
    graph ./output/simulation_credits.csv --title "Simulation Credits" -o ./output/simulation_credits.png
    graph ./output/simulation_latency.csv --title "Simulation Latency" -o ./output/simulation_latency.png
fi

echo -e "${GREEN}Simulation completed successfully!${NC}"
