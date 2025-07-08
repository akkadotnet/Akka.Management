#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$false)]
    [string]$recordType = "srv"
)

if ($recordType -ne "srv" -and $recordType -ne "a" -and $recordType -ne "aaaa") {
    Write-Error "Invalid record type. Must be 'srv' or 'a' or 'aaaa'."
    exit 1
}

if ($recordType -eq "a") {
    $recordType = "aaaa"
}

# Clean up previous containers first
podman-compose -f "$(pwd)/src/docker-compose.srv.yml" down
podman-compose -f "$(pwd)/src/docker-compose.aaaa.yml" down

# Build and publish the container
dotnet publish --os linux --arch x64 -c Release /t:PublishContainer ./src/DnsCluster.csproj

# Start with replace flag to handle container conflicts
podman-compose -f "$(pwd)/src/docker-compose.$recordType.yml" up --build 