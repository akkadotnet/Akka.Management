[CmdletBinding()]
Param(
    [Parameter(
            Mandatory=$True,
            HelpMessage = "Cluster node name")]
    [string] $NodeName,
    
    [Parameter(
            Mandatory=$True,
            HelpMessage = "Cluster size")]
    [int] $ClusterSize
)

$currentFolder = Get-Location
Set-Location $PSScriptRoot/docker
docker-compose up --scale $NodeName=$ClusterSize
Set-Location = $currentFolder