using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Amazon.S3;
using Docker.DotNet;
using Docker.DotNet.Models;
using Xunit;

namespace Akka.Discovery.AwsApi.Integration.Tests
{
    [CollectionDefinition("AwsSpec")]
    public sealed class AwsSpecsFixture: ICollectionFixture<LocalStackFixture>
    {
    }

    public sealed class LocalStackFixture : IAsyncLifetime
    {
        public const string BucketName = "bucket-test";
        private readonly string _containerName = $"localstack-{Guid.NewGuid():N}";
        private readonly DockerClient _client;

        public int Port { get; }

        public string Endpoint => $"http://localhost:{Port}";
        
        public AmazonCloudFormationClient CfCClient { get; private set; }
        public AmazonEC2Client Ec2Client { get; private set; }
        public AmazonS3Client S3Client { get; private set; }
        public List<string> IpAddresses { get; } = new List<string>();
        
        public LocalStackFixture()
        {
            DockerClientConfiguration config;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                config = new DockerClientConfiguration(new Uri("unix://var/run/docker.sock"));
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                config = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine"));
            else
                throw new NotSupportedException($"Unsupported OS [{RuntimeInformation.OSDescription}]");

            _client = config.CreateClient();
            
            var rnd = new Random();
            Port = rnd.Next(9000, 10000);
        }
        
        private const string ImageName = "localstack/localstack";
        private const string Tag = "latest";
        private readonly string _localStackImageName = $"{ImageName}:{Tag}";

        public async Task InitializeAsync()
        {
            var images = await _client.Images.ListImagesAsync(new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    {"reference", new Dictionary<string, bool> {{_localStackImageName, true}}}
                }
            });
            if (images.Count == 0)
                await _client.Images.CreateImageAsync(
                    new ImagesCreateParameters {FromImage = ImageName, Tag = Tag}, 
                    new AuthConfig(),
                    new Progress<JSONMessage>(message =>
                    {
                        Console.WriteLine(!string.IsNullOrEmpty(message.ErrorMessage)
                            ? message.ErrorMessage
                            : $"{message.ID} {message.Status} {message.ProgressMessage}");
                    }));

            // create the container
            await _client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = ImageName,
                Name = _containerName,
                Tty = true,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    {"4566", default},
                },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            "4566", new List<PortBinding> {new PortBinding {HostPort = $"{Port}"}}
                        },
                    }
                },
                Env = new List<string>
                {
                    "AWS_ACCESS_KEY_ID=test",
                    "AWS_SECRET_ACCESS_KEY=test",
                    "DEBUG=1",
                }
            });

            // start the container
            await _client.Containers.StartContainerAsync(_containerName, new ContainerStartParameters());
            
            // Wait until LocalStack is completely ready
            var logStream = await _client.Containers.GetContainerLogsAsync(_containerName, new ContainerLogsParameters
            {
                Follow = true,
                ShowStdout = true,
                ShowStderr = true
            });

            string line = null;
            var timeoutInMilis = 60000;
            using (var reader = new StreamReader(logStream))
            {
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.ElapsedMilliseconds < timeoutInMilis && (line = await reader.ReadLineAsync()) != null)
                {
                    if (line == "Ready.")
                    {
                        break;
                    }
                }
                stopwatch.Stop();
            }
            await logStream.DisposeAsync();
            if (line != "Ready.")
                throw new Exception("LocalStack docker image failed to run.");
            
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
                SubnetId = publicSubnetId,
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
                SubnetId = privateSubnetId,
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
            if (_client != null)
            {
                // Delay to make sure that all tests has completed cleanup.
                await Task.Delay(TimeSpan.FromSeconds(5));
    
                // Kill the container, we can't simply stop the container because Redis can hung indefinetly
                // if we simply stop the container.
                await _client.Containers.KillContainerAsync(_containerName, new ContainerKillParameters());
    
                await _client.Containers.RemoveContainerAsync(_containerName,
                    new ContainerRemoveParameters {Force = true});
                _client.Dispose();
            }
        }
    }
}