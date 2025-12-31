# To run this, install dependencies: pip install -r requirements.txt

from diagrams import Diagram, Cluster, Edge
from diagrams.aws.compute import EC2
from diagrams.aws.database import RDS
from diagrams.aws.network import InternetGateway
from diagrams.aws.security import IAM, SecretsManager
from diagrams.aws.management import Cloudwatch
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

with Diagram("Demo Architecture", show=False, 
             graph_attr=graph_attr, 
             node_attr=node_attr, 
             edge_attr=edge_attr) as diag:

    # ==========================================
    # 2. EXTERNAL & SECURITY RESOURCES
    # ==========================================
    
    with Cluster("External"):
        grafana = Grafana("Grafana")

    with Cluster("Security Config"):
        ssm_role = IAM("SSM Role")
        grafana_secret = SecretsManager("Secret Manager")

    # ==========================================
    # 3. AWS VPC INFRASTRUCTURE
    # ==========================================
    with Cluster("Demo VPC"):
        igw = InternetGateway("Internet Gateway")
        
        # 3a. PUBLIC ZONE (The Entry Point)
        with Cluster("Public Subnet"):
            # Labeled functionally as discussed
            attacker = EC2("Load Generator\n(k6 + Node.js API)")

        # 3b. PRIVATE ZONE (The Hardened Layer)
        with Cluster("Private Subnets"):
            # Labeled to highlight the hardening
            victim_db = RDS("Target DB\n(db.t3.medium)")

    # ==========================================
    # 4. MONITORING BACKEND
    # ==========================================
    cw = Cloudwatch("CloudWatch")

    # ==========================================
    # 5. DATA FLOWS & CONNECTIONS
    # ==========================================

    grafana_secret - Edge(style="invis", minlen="1") - igw

    # A. Setup & Access
    igw >> Edge(color="black", penwidth="4") >> attacker
    ssm_role - Edge(style="dotted", color="black", penwidth="4") - attacker

    # B. The Attack (The Critical Path)
    attacker >> Edge(
        color="firebrick", 
        penwidth="4", 
        minlen="2"
    ) >> victim_db
    
    # C. Monitoring Flow
    # 1. RDS pushes telemetry to CloudWatch
    victim_db >> Edge(style="dashed", color="darkgreen", minlen="1", penwidth="4") >> cw
    
    # 2. Grafana pulls from CloudWatch
    grafana >> Edge(
        color="darkgreen", 
        penwidth="4"
    ) >> cw

    # 3. Identity flow
    grafana >> Edge(style="dashed", color="black", penwidth="4") >> grafana_secret

diag