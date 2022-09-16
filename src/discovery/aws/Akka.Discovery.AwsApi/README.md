# AWS API Based Discovery

If you’re using EC2 directly or you’re using ECS with host mode networking and you’re deploying one container per cluster member, continue to [AWS EC2 Tag-Based Discovery](#aws-ec2-tag-based-discovery)

If you’re using ECS with [awsvpcs](https://aws.amazon.com/blogs/compute/introducing-cloud-native-networking-for-ecs-containers/) mode networking (whether on EC2 or with [Fargate](https://aws.amazon.com/fargate/)), continue to [AWS ECS Discovery](#aws-ecs-discovery).

ECS with bridge mode networking is not supported.

# AWS EC2 Tag-Based Discovery

This module can be used as a discovery method for an AWS EC2 based cluster.

You can use tags to simply mark the instances that belong to the same cluster. Use a tag that has “service” as the key and set the value equal to the name of your service.

Note that this implementation is adequate for users running service clusters on vanilla EC2 instances. These instances can be created and tagged manually, or created via an auto-scaling group (ASG). If they are created via an ASG, they can be tagged automatically on creation. Simply add the tag to the auto-scaling group configuration and ensure the “Tag New Instances” option is checked.

## Configuring using [Akka.Hosting](https://github.com/akkadotnet/Akka.Hosting)

You can add `Akka.Discovery.AwsApi.Ec2` support by using the `WithAwsEc2Discovery` extension method.

```csharp
builder.WithAwsEc2Discovery();
```

or

```csharp
builder.WithAwsEc2Discovery(setup => {
    setup.WithCredentialProvider<AnonymousEc2CredentialProvider>();
    setup.TagKey = "myTag";
});
```

or

```csharp
builder.WithAwsEc2Discovery(new Ec2ServiceDiscoverySetup {
    TagKey = "myTag"
});
```

## Configuring using HOCON configuration

To use `Akka.Discovery.AwsApi` in your project will need to include these HOCON settings in your HOCON configuration:

```
akka.discovery {
  # Set the following in your application.conf if you want to use this discovery mechanism:
  # method = aws-api-ec2-tag-based
  aws-api-ec2-tag-based {
    class = "Akka.Discovery.AwsApi.Ec2.Ec2TagBasedServiceDiscovery, Akka.Discovery.AwsApi"

    # Fully qualified class name of a class that extends Akka.Discovery.AwsApi.Ec2.Ec2ConfigurationProvider with either 
    # a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
    client-config = ""
    
    credentials-provider = instance-metadata-credential-provider

    tag-key = "service"

    # filters have to be in key=value format, separated by semi-colon
    filters = ""

    # If you want multiple akka nodes (i.e. JVMs) per EC2 instance, set the following
    # to the list of Akka Management port numbers
    ports = []

    # client may use specified endpoint for example ec2.us-west-1.amazonaws.com
    # region is automatically extrapolated from the endpoint URL
    # endpoint = ""
    
    # client may use specified region for example us-west-1
    # endpoints are automatically generated.
    # NOTE: You can only set either an endpoint OR a region, not both.
    #       Region will always win if both are declared.
    # region = ""
    
    anonymous-credential-provider {
        # Fully qualified class name of a class that extends Akka.Discovery.AwsApi.Ec2.Ec2ConfigurationProvider with either 
        # a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
        class = "Akka.Discovery.AwsApi.Ec2.AnonymousEc2CredentialProvider, Akka.Discovery.AwsApi"    
    }
    
    # This configuration provider leverages the EC2 instance metadata service to provide connection
    # information for the AWS EC2 client.
    # Please read https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/configuring-instance-metadata-service.html
    # on AWS EC2 metadata profile setup.
    instance-metadata-credential-provider {
        # Fully qualified class name of a class that extends Akka.Discovery.AwsApi.Ec2.Ec2ConfigurationProvider with either 
        # a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
        class = "Akka.Discovery.AwsApi.Ec2.Ec2InstanceMetadataCredentialProvider, Akka.Discovery.AwsApi"
        
        # Name of the Amazon IAM Role to be used as credential provider
        # If null or entry, the first returned credential is used.
        role = ""
    }    
  }
}
```

## Using Discovery Together with Akka.Management and Cluster.Bootstrap
All discovery plugins are designed to work with _Cluster.Bootstrap_ to provide an automated way to form a cluster that is not based on hard wired seeds configuration. 

### Configuring with [Akka.Hosting](https://github.com/akkadotnet/Akka.Hosting)

Auto-starting _Akka.Management_, _Akka.Management.Cluster.Bootstrap_, and `Akka.Discovery.AwsApi.Ec2`

```csharp
using var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddAkka("ec2BootstrapDemo", (builder, provider) =>
        {
            builder
                .WithRemoting("", 4053)
                .WithClustering()
                .WithClusterBootstrap(serviceName: "testService")
                .WithAwsEc2Discovery();
        });
    }).Build();

await host.RunAsync();
```

Manually starting _Akka.Management_, _Akka.Management.Cluster.Bootstrap_ and `Akka.Discovery.AwsApi.Ec2`

```csharp
using var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddAkka("ec2BootstrapDemo", (builder, provider) =>
        {
            builder
                .WithRemoting("", 4053)
                .WithClustering()
                .WithAkkaManagement()
                .WithClusterBootstrap(
                    serviceName: "testService",
                    autoStart: false)
                .WithAwsEc2Discovery();
            
            builder.AddStartup(async (system, registry) =>
            {
                await AkkaManagement.Get(system).Start();
                await ClusterBootstrap.Get(system).Start();
            });
        });
    }).Build();

await host.RunAsync();
```

> __NOTE__
>
> In order for for EC2 Discovery to work, you also need open _Akka.Management_ port on your EC2 instances (8558 by default)

### Configuring with HOCON Configuration

Some HOCON configuration is needed to make discovery work with Cluster.Bootstrap:

```
akka.discovery.method = aws-api-ec2-tag-based
akka.management.http.routes = {
    cluster-bootstrap = "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap"
}
```

You then start the cluster bootstrapping process by calling:
```C#
await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();
```

A more complete example:
```C#
var config = ConfigurationFactory
    .ParseString(File.ReadAllText("application.conf"))
    .WithFallback(ClusterBootstrap.DefaultConfiguration())
    .WithFallback(AkkaManagementProvider.DefaultConfiguration());

var system = ActorSystem.Create("my-system", config);
var log = Logging.GetLogger(system, this);

await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();

var cluster = Cluster.Get(system);
cluster.RegisterOnMemberUp(() => {
  var upMembers = cluster.State.Members
      .Where(m => m.Status == MemberStatus.Up)
      .Select(m => m.Address.ToString());

  log.Info($"Current up members: [{string.Join(", ", upMembers)}]")
});
```

> __NOTE__
> 
> In order for for EC2 Discovery to work, you also need open _Akka.Management_ port on your EC2 instances (8558 by default)

## Configuration

### EC2 Client Configuration

You can extend the `Amazon.EC2.AmazonEC2Config` class to provide your own configuration implementation for the internal EC2 client; the extended class can have either an empty constructor or a constructor that takes an ExtendedActorSystem as a parameter. 

To have the discovery module to use your configuration implementation you can either pass the type into `Ec2ServiceDiscoverySetup` if you are using _Akka.Hosting_ or provide the fully qualified class name of your implementation in the _akka.discovery.aws-api-ec2-tag-based.client-config_ property inside the HOCON configuration.

- **Akka.Hosting**:

  ```csharp
  builder.WithAwsEc2Discovery(setup => {
    setup.ClientConfig = typeof(MyAmazonEc2Config);
  });
  ```

- **HOCON**:
  ```
  akka.discovery {
    aws-api-ec2-tag-based {
      client-config = "MyAkkaAssembly.MyAmazonEc2Config, MyAkkaAssembly"
    }
  }
  ```

### EC2 Client Credentials Configuration

Client credentials are provided by the `Akka.Discovery.AwsApi.Ec2.Ec2CredentialProvider` abstract class. There are two implementation provided out of the box, `AnonymousEc2CredentialProvider` and `Ec2InstanceMetadataCredentialProvider`.

#### Anonymous Ec2 Credential Provider

`AnonymousEc2CredentialProvider` is a very simple credential provider and will return an [`AnonymousAWSCredentials`](https://docs.aws.amazon.com/sdkfornet1/latest/apidocs/html/T_Amazon_Runtime_AnonymousAWSCredentials.htm).

#### Ec2 Instance Metadata Service Credential Provider

`Ec2InstanceMetadataCredentialProvider` will try its best to retrieve the correct session credential provider using the AWS EC2 Instance Metadata Service (IMDS) API. It will return an [`AnonymousAWSCredential`](https://docs.aws.amazon.com/sdkfornet1/latest/apidocs/html/T_Amazon_Runtime_AnonymousAWSCredentials.htm) if it fails to obtain a credential from the metadata API service.

> __WARNING__
> 
> This method will only work if: 
> - The discovery service is running inside an EC2 instance 
> - The EC2 instance metadata service is __NOT__ disabled (AWS_EC2_METADATA_DISABLED environment variable is __NOT__ set)
> - The IAM role of the instance is properly set, and
> - The metadata options of the instance is set to use the IMDSv2 metadata service.

#### Custom Credential Provider

To create a custom credential provider, you can extend the `Akka.Discovery.AwsApi.Ec2.Ec2CredentialProvider` abstract class. You then reference the fully qualified class name of your custom provider.

- **Akka.Hosting**:

  ```csharp
   builder.WithAwsEc2Discovery(setup => {
    setup.CredentialsProvider = typeof(CustomCredentialProvider);
  });
  ```

- **HOCON**:

  ```text
  akka.discovery {
    aws-api-ec2-tag-based {
      credentials-provider = "MyAkkaAssembly.CustomCredentialProvider, MyAkkaAssembly"
    }
  }
  ```

## Notes
- You will need to make sure that the proper privileges are in place for the discovery implementation to access the Amazon EC2 API. The simplest way to do this is by creating a IAM role that, at a minimum, allows the DescribeInstances action. Attach this IAM role to the EC2 instances that need to access the discovery implementation. See the [docs for IAM Roles for Amazon EC2](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/iam-roles-for-amazon-ec2.html).
- In general, for the EC2 instances to “talk to each other” (necessary for forming a cluster), they need to be in the same security group and [the proper rules have to be set](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/security-group-rules-reference.html#sg-rules-other-instances).
- You can set additional filters (by instance type, region, other tags etc.).
  - **Akka.Hosting**
    ```csharp
    builder.WithAwsEc2Discovery(setup => {
      setup.Filters = new List<Filter> {
        new ("instance-type", new List<string>{ "m1.small" }),
        new ("tag:purpose", new List<string>{ "production" }),
      };
    })  
    ```
  - **HOCON**

    _akka.discovery.aws-api-ec2-tag-based.filters_. The filters have to be key=value pairs separated by the semicolon character. For example:
    ```
    akka.discovery.aws-api-ec2-tag-based {
      filters = "instance-type=m1.small;tag:purpose=production"
    }
    ```
- By default, this module is configured for clusters with one Akka node per EC2 instance: it separates cluster members solely by their EC2 IP address. However, we can change the default configuration to indicate multiple ports per discovered EC2 IP, and achieve a setup with multiple Akka nodes per EC2 instance.
  - **Akka.Hosting**
    ```csharp
    builder.WithAwsEc2Discovery(setup => {
      setup.Ports = new List<int> { 8557, 8558, 8559 };
    });
    ```
  - **HOCON**
    ```
    akka.discovery.aws-api-ec2-tag-based {
      ports = [8557, 8558, 8559] # 3 Akka nodes per EC2 instance
    }
    ```
  Note: this comes with the limitation that each EC2 instance has to have the same number of Akka nodes.
- You can change the default tag key from “service” to something else. 
  - **Akka.Hosting**

    ```csharp
    builder.WithAwsEc2Discovery(setup => {
      setup.TagKey = "akka-cluster";
    }); 
    ```
    
  - **HOCON**

    ```
    akka.discovery.aws-api-ec2-tag-based {
      tag-key = "akka-cluster"
    }
    ```

### I'm getting an `AmazonEC2Exception: The request must contain the parameter AWSAccessKeyId`

Please consider using the `Ec2InstanceMetadataCredentialProvider` credential provider if your discovery service lives inside an EC2 instance. Make sure that your instance is assigned to a proper role and the Instance Metadata Service (IMDS) version is set to IMDSv2.

You can check to see if the metadata service is running by running curl inside your instance:
```
curl http://169.254.169.254
```
It should return a 200-OK if the metadata service is running.

https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/configuring-instance-metadata-service.html

# AWS ECS Discovery

If you’re using ECS with [awsvpcs](https://aws.amazon.com/blogs/compute/introducing-cloud-native-networking-for-ecs-containers/) mode networking, you can have all task instances of a given ECS service discover each other. 

- **Akka.Hosting**

  ```csharp
  builder.WithAwsEcsDiscovery(clusterName: "your-ecs-cluster-name");
  ```

- **HOCON**
  ```text
  akka.discovery {
    method = aws-api-ecs
    aws-api-ecs {
      # Defaults to "default" to match the AWS default cluster name if not overridden
      cluster = "your-ecs-cluster-name"
    }
  }
  ```

## Using Discovery Together with Akka.Management and Cluster.Bootstrap

For _Cluster.Bootstrap_, you will need to set `ClusterBootstrapSetup.ContactPointDiscovery.ServiceName` or _akka.management.cluster.bootstrap.contact-point-discovery.service-name_ HOCON setting to the ECS service name.

- **Akka.Hosting**
  
  ```csharp
  builder.WithClusterBootstrap(setup =>
    {
        setup.ContactPointDiscovery.ServiceName = "your-ecs-service-name";
    }, autoStart: true);
  ```
  
- **HOCON**
  ```text
  akka.management.cluster.bootstrap.contact-point-discovery {
    service-name = "your-ecs-service-name"
  }
  ```

## Notes

- Since the implementation uses the AWS ECS API, you’ll need to make sure that AWS credentials are provided. The simplest way to do this is to create an IAM role that includes appropriate permissions for AWS ECS API access. Attach this IAM role to the task definition of the ECS Service. See the docs for [IAM Roles for Tasks](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-iam-roles.html).
- In general, for the ECS task instances to “talk to each other” (necessary for forming a cluster), they need to be in the same security group and the proper rules have to be set. See the docs for Task Networking with the awsvpc Network Mode.
- Because of how _awsvpc_ mode is set up, _Akka.Remote_ and _Akka.Management_ may not be able to automatically determine the host address via DNS resolution. To address this, you will need to set these settings manually using the provided `AwsEcsDiscovery.GetContainerAddress()` static utility method. Please check the [code example](https://github.com/akkadotnet/Akka.Management/blob/798a73fb84288294cd82d6697f0e943acac9f2cb/src/discovery/examples/Aws.Ecs/Program.cs#L51-L66) on how to do this.
- Because ECS service discovery can only discover IP addresses (not ports) you’ll need to set _akka.management.cluster.bootstrap.contact-point.fallback-port = 8558_, where 8558 is whatever port you choose to bind _Akka.Management_ to.
- You can set additional filters to only discover nodes with specific tag values in your application.conf file, in the akka.discovery.aws-api-ecs-async.tags key. An empty list of tags will not filter any nodes out.

  For example:
  ```text
  akka.discovery.aws-api-ecs-async { 
    tags = [ 
      { key = "environment", value = "staging" }, 
      { key = "deployment-side", value = "blue" } 
    ] 
  }
  ```
   
- The current implementation only supports discovery of service task instances within the same region.
