#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$false)]
    [string]$recordType = "srv"
)

if ($recordType -ne "srv" -and $recordType -ne "a" -and $recordType -ne "aaaa") {
    Write-Error "Invalid record type. Must be 'srv' or 'a' or 'aaaa'."
    exit 1
}
Write-Output "Cleaning up previous containers (ignore errors if containers do not exist)...."
docker-compose -f "$(pwd)/src/docker-compose.srv.yml" down
docker-compose -f "$(pwd)/src/docker-compose.aaaa.yml" down
docker-compose -f "$(pwd)/src/docker-compose.a.yml" down

Write-Output "Building and publishing example..."
dotnet publish --os linux --arch x64 -c Release /t:PublishContainer ./src/DnsCluster.csproj

Write-Output "Start $recordType example"
docker-compose -f "$(pwd)/src/docker-compose.$recordType.yml" up --build 