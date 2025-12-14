from diagrams import Diagram, Cluster, Edge
from diagrams.aws.compute import EC2
from diagrams.aws.database import RDS
from diagrams.aws.network import VPC, InternetGateway, PublicSubnet, PrivateSubnet
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
    "fontsize": "12",
    "fontname": "Sans-Serif",
    "penwidth": "2.0"
}

with Diagram("CIS-Aligned T3 Demo Architecture", show=False, 
             graph_attr=graph_attr, 
             node_attr=node_attr, 
             edge_attr=edge_attr) as diag:

    # ==========================================
    # 2. EXTERNAL & SECURITY RESOURCES
    # ==========================================
    
    with Cluster("External"):
        grafana = Grafana("Grafana Cloud\n(Observability)")

    with Cluster("Security Config"):
        # Stacking these vertically
        ssm_role = IAM("SSM Role\n(Session Manager)")
        grafana_secret = SecretsManager("Grafana\nSecret")

    # ==========================================
    # 3. AWS VPC INFRASTRUCTURE
    # ==========================================
    with Cluster("Demo VPC (10.0.0.0/16)"):
        igw = InternetGateway("Internet Gateway")
        
        # 3a. PUBLIC ZONE (The Entry Point)
        with Cluster("Public Subnet A\n(Internet Access)"):
            # Labeled functionally as discussed
            attacker = EC2("Load Generator\n(k6 + Node.js Proxy)")

        # 3b. PRIVATE ZONE (The Hardened Layer)
        with Cluster("Private Subnets\n(Isolated Data Layer)"):
            # Labeled to highlight the hardening
            victim_db = RDS("Target DB\n(T3 Unlimited)\n[Encrypted]")

    # ==========================================
    # 4. MONITORING BACKEND
    # ==========================================
    cw = Cloudwatch("CloudWatch\nMetrics")

    # ==========================================
    # 5. DATA FLOWS & CONNECTIONS
    # ==========================================

    grafana_secret - Edge(style="invis", minlen="1") - igw

    # A. Setup & Access
    igw >> Edge(color="gray") >> attacker
    ssm_role - Edge(style="dotted", color="gray") - attacker

    # B. The Attack (The Critical Path)
    attacker >> Edge(
        label="Expensive SQL\n(Cross Join)", 
        color="firebrick", 
        penwidth="3.0", 
        minlen="2"
    ) >> victim_db
    
    # C. Monitoring Flow
    # 1. RDS pushes telemetry to CloudWatch
    victim_db >> Edge(style="dashed", color="black", minlen="1") >> cw
    
    # 2. Grafana pulls from CloudWatch
    grafana >> Edge(
        label="Query", 
        color="darkgreen", 
        penwidth="2.5"
    ) >> cw

    # 3. Identity flow
    grafana >> Edge(style="dashed", color="gray", label="Read Secret") >> grafana_secret

diag