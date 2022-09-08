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

">>>> Retrieving VPC ID"
$vpcId = aws ec2 describe-vpcs `
    --region $region `
    --filters Name=isDefault,Values=true `
    --output text `
    --query "Vpcs[0].VpcId" `
    --debug

">>>> Retrieving subnets"
$subnets = aws ec2 describe-subnets `
    --region $region `
    --filter `
      Name=vpcId,Values=$vpcId `
      Name=defaultForAz,Values=true `
    --output text `
    --query "Subnets[].SubnetId | join(',', @)" `
    --debug

$dir = $PSScriptRoot -replace '\\', '/'
">>>> Executing CloudFormation file"
aws cloudformation "$operation-stack" `
    --region $region `
    --stack-name ecs-integration-test-app `
    --template-body "file://$dir/../cfn-templates/ecs-integration-test-app.yaml" `
    --capabilities CAPABILITY_IAM `
    --parameters `
      ParameterKey=Subnets,ParameterValue="'$subnets'" `
    --debug

aws cloudformation wait "stack-$operation-complete" `
    --region $region `
    --stack-name ecs-integration-test-app `
    --debug
