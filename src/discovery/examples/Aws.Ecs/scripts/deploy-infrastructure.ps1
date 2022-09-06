[CmdletBinding()]
Param(
    [Parameter(
            Mandatory,
            HelpMessage = "Operation type, either update or create",
            Position=0)]
    [ValidateSet("create","update")]
    [string] $operation,

    [Parameter(
            Mandatory,
            HelpMessage = "AWS region, example: us-east-1, us-east-2",
            Position=1)]
    [string] $region
)

$dir = $PSScriptRoot -replace '\\', '/'
">>>> Creating ECR resource"
aws cloudformation "$operation-stack" `
    --region $region `
    --stack-name ecs-integration-test-app-infrastructure `
    --template-body "file://$dir/../cfn-templates/ecs-integration-test-app-infrastructure.yaml" `
    --debug

aws cloudformation wait "stack-$operation-complete" `
    --region $region `
    --stack-name ecs-integration-test-app-infrastructure
