//-----------------------------------------------------------------------
// <copyright file="LocalStackFixture.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Amazon.S3;
using Docker.DotNet;
using Testcontainers.LocalStack;
using Xunit;

namespace Akka.Discovery.AwsApi.Integration.Tests;

[CollectionDefinition("AwsSpec")]
public sealed class AwsSpecsFixture: ICollectionFixture<LocalStackFixture>
{
}

public sealed class LocalStackFixture: IAsyncLifetime
{
    private readonly LocalStackContainer _container;

    public bool IsWindows { get; private set; }
    public string Endpoint { get; private set; }
    public AmazonCloudFormationClient? CfCClient { get; private set; }
    public AmazonEC2Client? Ec2Client { get; private set; }
    public AmazonS3Client? S3Client { get; private set; }
    public List<string> IpAddresses { get; } = new ();
        
    public LocalStackFixture()
    {
        _container = new LocalStackBuilder()
            .WithImage("localstack/localstack:3.6.0")
            .Build();
    }

    public async Task InitializeAsync()
    {
        using (var client = new DockerClientConfiguration().CreateClient())
        {
            var sysInfo = await client.System.GetSystemInfoAsync();
            var osType = sysInfo.OSType;
            IsWindows = osType is "windows";
        }

        if (IsWindows)
        {
            Endpoint = string.Empty;
            return;
        }
        
        await _container.StartAsync();
        Endpoint = _container.GetConnectionString();
            
        // Create clients
        S3Client = new AmazonS3Client(
            new BasicAWSCredentials("test", "test"), 
            new AmazonS3Config
            {
                ServiceURL = Endpoint,
                ForcePathStyle = true,
                Timeout = TimeSpan.FromSeconds(5)
            });
            
        CfCClient = new AmazonCloudFormationClient(new AmazonCloudFormationConfig
        {
            ServiceURL = Endpoint
        });
            
        Ec2Client = new AmazonEC2Client(
            new BasicAWSCredentials("test", "test"), 
            new AmazonEC2Config
            {
                ServiceURL = Endpoint,
            });
            
        // Create VPC
        var vpcResponse = await Ec2Client.CreateVpcAsync(new CreateVpcRequest
        {
            CidrBlock = "10.0.0.0/16"
        });
        var vpcId = vpcResponse.Vpc.VpcId;

        #region Public side

        // Create public subnet using VPC ID
        var subnetResponse = await Ec2Client.CreateSubnetAsync(new CreateSubnetRequest
        {
            VpcId = vpcId,
            CidrBlock = "10.0.1.0/24"
        });
        var publicSubnetId = subnetResponse.Subnet.SubnetId;
            
        // Create internet gateway
        var netGatewayResponse = await Ec2Client.CreateInternetGatewayAsync(new CreateInternetGatewayRequest());
        var internetGatewayId = netGatewayResponse.InternetGateway.InternetGatewayId;
            
        // Attach internet gateway to VPC
        await Ec2Client.AttachInternetGatewayAsync(new AttachInternetGatewayRequest
        {
            VpcId = vpcId,
            InternetGatewayId = internetGatewayId
        });
            
        // Create route table for the public subnet
        var routeTableResponse = await Ec2Client.CreateRouteTableAsync(new CreateRouteTableRequest
        {
            VpcId = vpcId
        });
        var publicRouteTableId = routeTableResponse.RouteTable.RouteTableId;

        // Create route to route all traffic to the internet gateway
        await Ec2Client.CreateRouteAsync(new CreateRouteRequest
        {
            RouteTableId = publicRouteTableId,
            GatewayId = internetGatewayId,
            DestinationCidrBlock = "0.0.0.0/0"
        });

        // Associate route table to public subnet
        await Ec2Client.AssociateRouteTableAsync(new AssociateRouteTableRequest
        {
            SubnetId = publicSubnetId,
            RouteTableId = publicRouteTableId
        });
            
        #endregion

        #region Private side

        // Create private subnet using VPC ID
        subnetResponse = await Ec2Client.CreateSubnetAsync(new CreateSubnetRequest
        {
            VpcId = vpcId,
            CidrBlock = "10.0.2.0/24"
        });
        var privateSubnetId = subnetResponse.Subnet.SubnetId;
            
        // Create NAT address
        var addressResult = await Ec2Client.AllocateAddressAsync(new AllocateAddressRequest{Domain = DomainType.Vpc});
        var addressAllocationId = addressResult.AllocationId;
            
        // Create NAT gateway
        var natGatewayResponse = await Ec2Client.CreateNatGatewayAsync(new CreateNatGatewayRequest
        {
            SubnetId = publicSubnetId,
            AllocationId = addressAllocationId
        });
        var natGatewayId = natGatewayResponse.NatGateway.NatGatewayId;
            
        // Create route table for the private subnet
        routeTableResponse = await Ec2Client.CreateRouteTableAsync(new CreateRouteTableRequest
        {
            VpcId = vpcId
        });
        var privateRouteTableId = routeTableResponse.RouteTable.RouteTableId;
            
        // Create route to route traffic to the NAT gateway
        await Ec2Client.CreateRouteAsync(new CreateRouteRequest
        {
            RouteTableId = privateRouteTableId,
            GatewayId = natGatewayId,
            DestinationCidrBlock = "0.0.0.0/0"
        });
            
        // Associate route table to private subnet
        await Ec2Client.AssociateRouteTableAsync(new AssociateRouteTableRequest
        {
            SubnetId = privateSubnetId,
            RouteTableId = privateRouteTableId
        });

        #endregion

        #region Security group

        // Create security group for the VPC
        var groupResponse = await Ec2Client.CreateSecurityGroupAsync(new CreateSecurityGroupRequest
        {
            GroupName = "akka-cluster",
            Description = "Akka cluster security group",
            VpcId = vpcId
        });
        var securityGroupId = groupResponse.GroupId;
            
        // Open port 22
        await Ec2Client.AuthorizeSecurityGroupIngressAsync(new AuthorizeSecurityGroupIngressRequest
        {
            GroupId = securityGroupId,
            IpPermissions = new List<IpPermission>
            {
                new IpPermission
                {
                    IpProtocol = "tcp",
                    FromPort = 22,
                    ToPort = 22,
                    Ipv4Ranges = new List<IpRange>
                    {
                        new IpRange
                        {
                            CidrIp = "0.0.0.0/0"
                        }
                    }
                }
            }
        });
            
        // Open port 80
        await Ec2Client.AuthorizeSecurityGroupIngressAsync(new AuthorizeSecurityGroupIngressRequest
        {
            GroupId = securityGroupId,
            IpPermissions = new List<IpPermission>
            {
                new IpPermission
                {
                    IpProtocol = "tcp",
                    FromPort = 80,
                    ToPort = 80,
                    Ipv4Ranges = new List<IpRange>
                    {
                        new IpRange
                        {
                            CidrIp = "0.0.0.0/0"
                        }
                    }
                }
            }
        });
        #endregion
            
        // Create key-pair
        var keyName = "cli-keyPair";
        var keyPairResponse = await Ec2Client.CreateKeyPairAsync(new CreateKeyPairRequest
        {
            KeyName = keyName
        });
        var keyPairMaterial = keyPairResponse.KeyPair.KeyMaterial; // the PEM

        var instanceResponse = await Ec2Client.RunInstancesAsync(new RunInstancesRequest
        {
            ImageId = "ami-fake",
            InstanceType = InstanceType.T2Micro,
            MinCount = 2,
            MaxCount = 2,
            SecurityGroupIds = new List<string> {securityGroupId},
            KeyName = keyName,
            NetworkInterfaces = new List<InstanceNetworkInterfaceSpecification>
            {
                new InstanceNetworkInterfaceSpecification
                {
                    DeviceIndex = 0,
                    AssociatePublicIpAddress = true,
                    SubnetId = publicSubnetId
                }
            }
        });
        var instanceIds = new List<string>();
        foreach (var instance in instanceResponse.Reservation.Instances)
        {
            instanceIds.Add(instance.InstanceId);
            IpAddresses.Add(instance.PrivateIpAddress);
        }
            
        instanceResponse = await Ec2Client.RunInstancesAsync(new RunInstancesRequest
        {
            ImageId = "ami-fake",
            InstanceType = InstanceType.T2Micro, 
            MinCount = 2,
            MaxCount = 2,
            SecurityGroupIds = new List<string> {securityGroupId},
            KeyName = keyName,
            NetworkInterfaces = new List<InstanceNetworkInterfaceSpecification>
            {
                new InstanceNetworkInterfaceSpecification
                {
                    DeviceIndex = 0,
                    AssociatePublicIpAddress = true,
                    SubnetId = privateSubnetId
                }
            }
        });
        foreach (var instance in instanceResponse.Reservation.Instances)
        {
            instanceIds.Add(instance.InstanceId);
            IpAddresses.Add(instance.PrivateIpAddress);
        }

        // Tag instance with service name
        await Ec2Client.CreateTagsAsync(new CreateTagsRequest
        {
            Resources = instanceIds,
            Tags = new List<Tag>
            {
                new Tag
                {
                    Key = "service",
                    Value = "fake-api"
                }
            }
        });

        await Ec2Client.ModifyVpcAttributeAsync(new ModifyVpcAttributeRequest
        {
            VpcId = vpcId,
            EnableDnsHostnames = true
        });            
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}