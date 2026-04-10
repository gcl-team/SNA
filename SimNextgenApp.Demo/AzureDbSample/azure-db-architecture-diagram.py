# To run this, install dependencies: pip install -r requirements.txt

from diagrams import Diagram, Cluster, Edge
from diagrams.azure.compute import VM
from diagrams.azure.database import DatabaseForPostgresqlServers
from diagrams.azure.network import VirtualNetworks, PublicIpAddresses
from diagrams.azure.identity import ManagedIdentities
from diagrams.azure.security import KeyVaults
from diagrams.azure.monitor import Monitor
from diagrams.onprem.monitoring import Grafana

# ==========================================
# 1. VISUAL CONFIGURATION
# ==========================================
graph_attr = {
    "fontsize": "24",           # Title font size
    "bgcolor": "white",         # Background color
    "splines": "ortho",         # Orthogonal lines (90 degree angles)
    "nodesep": "1.0",           # Horizontal separation between nodes
    "ranksep": "1.0",           # Vertical separation between groups
    "pad": "0.5",               # Padding around the image
    "dpi": "300"                # High resolution (prevents blurriness in Slides)
}

node_attr = {
    "fontsize": "14",
    "fontname": "Sans-Serif",
    "fontweight": "bold"
}

edge_attr = {
    "fontsize": "14",
    "fontname": "Sans-Serif",
    "penwidth": "2.0"
}

with Diagram("Azure Database Demo Architecture", show=False,
             graph_attr=graph_attr,
             node_attr=node_attr,
             edge_attr=edge_attr) as diag:

    # ==========================================
    # 2. EXTERNAL & SECURITY RESOURCES
    # ==========================================

    with Cluster("External"):
        grafana = Grafana("Grafana Cloud")

    with Cluster("Security Config"):
        managed_id = ManagedIdentities("Managed Identity")
        key_vault = KeyVaults("Key Vault")

    # ==========================================
    # 3. AZURE VNET INFRASTRUCTURE
    # ==========================================
    with Cluster("Demo VNet"):
        public_ip = PublicIpAddresses("Public IP")
        vnet = VirtualNetworks("Virtual Network")

        # 3a. PUBLIC SUBNET (The Entry Point)
        with Cluster("Public Subnet"):
            # Load generator VM
            load_gen = VM("Load Generator\n(k6 + Node.js API)")

        # 3b. PRIVATE SUBNET (The Hardened Layer)
        with Cluster("Private Subnet"):
            # Azure Database for PostgreSQL Flexible Server
            target_db = DatabaseForPostgresqlServers("Target DB\n(B2ms - Burstable)")

    # ==========================================
    # 4. MONITORING BACKEND
    # ==========================================
    azure_monitor = Monitor("Azure Monitor")

    # ==========================================
    # 5. DATA FLOWS & CONNECTIONS
    # ==========================================

    key_vault - Edge(style="invis", minlen="1") - public_ip

    # A. Setup & Access
    public_ip >> Edge(color="black", penwidth="4") >> vnet
    vnet >> Edge(color="black", penwidth="4") >> load_gen
    managed_id - Edge(style="dotted", color="black", penwidth="4") - load_gen

    # B. The Simulation (The Critical Path)
    # High-frequency queries from load generator to database
    load_gen >> Edge(
        color="firebrick",
        penwidth="4",
        minlen="2",
        label="High Load\n(20 req/sec)"
    ) >> target_db

    # C. Monitoring Flow
    # 1. Database pushes telemetry to Azure Monitor
    target_db >> Edge(style="dashed", color="darkgreen", minlen="1", penwidth="4") >> azure_monitor

    # 2. Grafana Cloud pulls from Azure Monitor (or via OpenTelemetry)
    grafana >> Edge(
        color="darkgreen",
        penwidth="4",
        label="OpenTelemetry"
    ) >> azure_monitor

    # 3. Identity flow for secure access
    grafana >> Edge(style="dashed", color="black", penwidth="4") >> key_vault

diag
