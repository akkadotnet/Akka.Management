[CmdletBinding()]
Param(
    [Parameter(
            Mandatory,
            HelpMessage = "AWS region, example: us-east-1, us-east-2",
            Position=0)]
    [string] $region
)

$currentDir = Get-Location
Set-Location $PSScriptRoot/../

">>>> Publishing project"
dotnet publish -c Release

">>>> Retrieving AWS account name"
$account = aws sts get-caller-identity --output text --query "Account"

">>>> Logging in docker"
aws ecr get-login-password --region $region | docker login --username AWS --password-stdin "$account.dkr.ecr.$region.amazonaws.com"

">>>> Building docker image"
docker build -t ecs-integration-test-app .

">>>> Pushing image"
docker tag ecs-integration-test-app:latest "$account.dkr.ecr.$region.amazonaws.com/ecs-integration-test-app:latest"
docker push "$account.dkr.ecr.$region.amazonaws.com/ecs-integration-test-app:latest"

">>>> Removing local docker image"
docker rmi ecs-integration-test-app
docker rmi "$account.dkr.ecr.$region.amazonaws.com/ecs-integration-test-app:latest"

Set-Location $currentDir