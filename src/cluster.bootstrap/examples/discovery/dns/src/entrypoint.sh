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
echo "  PORTNAME: $PORTNAME"
echo "  DNS_PORT: $DNS_PORT"
echo "  DNS_NAMESERVER: $DNS_NAMESERVER"
echo "==================================="
DNS_PORT=${DNS_PORT:-53}
DNS_SERVER="${DNS_NAMESERVER:-127.0.0.1}"
echo "\nPerforming DNS resolution test for '$SERVICENAME' using DNS server $DNS_SERVER:$DNS_PORT..."
# A records
dig @$DNS_SERVER -p $DNS_PORT $SERVICENAME
# AAAA records
dig @$DNS_SERVER -p $DNS_PORT -t aaaa $SERVICENAME
# SRV records
dig @$DNS_SERVER -p $DNS_PORT -t srv "_${PORTNAME}._tcp.$SERVICENAME"

export NAMESERVER="${DNS_NAMESERVER:-127.0.0.1}:$DNS_PORT"
echo "\nStarting DnsCluster with DNS discovery..."
exec dotnet /app/DnsCluster.dll "$@"