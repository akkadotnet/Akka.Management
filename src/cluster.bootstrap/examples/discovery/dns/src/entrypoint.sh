#!/usr/bin/env bash
set -e

# Print container info for debugging
echo "==== AKKA DNS CLUSTER NODE STARTING ===="
echo "Hostname: $(hostname)"
echo "IP addresses: $(hostname -I)"
echo "Environment variables:"
echo "  CLUSTER__PORT: $CLUSTER__PORT"
echo "  CLUSTER__IP: $CLUSTER__IP"
echo "  MANAGEMENT__PORT: $MANAGEMENT__PORT"
echo "  ACTORSYSTEM: $ACTORSYSTEM"
echo "  SERVICENAME: $SERVICENAME"
echo "==================================="

# Check DNS resolution
echo "\nPerforming DNS resolution test for '$SERVICENAME'..."
dig $SERVICENAME

# Also try another DNS tool for verification
echo "\nNslookup verification:"
nslookup $SERVICENAME

echo "\nStarting DnsCluster with DNS discovery..."
exec dotnet /app/DnsCluster.dll "$@"