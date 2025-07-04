#!/usr/bin/env pwsh
# Clean up previous containers first
podman-compose -f "$(pwd)/src/docker-compose.yml" down

# Build and publish the container
dotnet publish --os linux --arch x64 -c Release /t:PublishContainer ./src/DnsCluster.csproj

# Start with replace flag to handle container conflicts
podman-compose -f "$(pwd)/src/docker-compose.yml" up --build 