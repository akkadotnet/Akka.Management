# AWS EC2 Tag-Based Discovery
This module can be used as a discovery method for an AWS EC2 based cluster.

You can use tags to simply mark the instances that belong to the same cluster. 
Use a tag that has “service” as the key and set the value equal to the name of your service.

Note that this implementation is adequate for users running service clusters on vanilla EC2 instances. 
These instances can be created and tagged manually, or created via an auto-scaling group (ASG). 
If they are created via an ASG, they can be tagged automatically on creation. 
Simply add the tag to the auto-scaling group configuration and ensure the “Tag New Instances” option is checked.

To use `Akka.Discovery.AwsApi` in your project, you must also include `Akka.Discovery` in your project nuget package dependency.
You will also need to include these HOCON settings in your HOCON configuration:
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
All discovery plugins are designed to work with Cluster.Bootstrap to provide an automated way to form a cluster that is not based
on hard wired seeds configuration. Some HOCON configuration is needed to make discovery work with Cluster.Bootstrap:

```
akka.discovery.method = aws-api-ec2-tag-based
akka.management.http.routes = {
    cluster-bootstrap = "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap"
}
```

You then start the cluster bootstrapping process by calling:
```
await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();
```

## Configuration
### EC2 Client Configuration
You can extend the `Amazon.EC2.AmazonEC2Config` class to provide your own configuration
implementation for the internal EC2 client; the extended class can have either an empty
constructor or a constructor that takes an ExtendedActorSystem as a parameter. 
You then provide the fully qualified class name of your implementation in the 
`akka.discovery.aws-api-ec2-tag-based.client-config` property inside the HOCON configuration.

### EC2 Client Credentials Configuration
Client credentials are provided by the `Akka.Discovery.AwsApi.Ec2.Ec2CredentialProvider` abstract
class. There are two implementation provided out of the box, `AnonymousEc2CredentialProvider` and
`Ec2InstanceMetadataCredentialProvider`.

#### Anonymous Ec2 Credential Provider
`AnonymousEc2CredentialProvider` is a very simple credential provider and will return
an `AnonymousAWSCredentials`.

#### Ec2 Instance Metadata Service Credential Provider
`Ec2InstanceMetadataCredentialProvider` will try its best to retrieve the correct session
credential provider using the AWS EC2 Instance Metadata Service (IMDS) API. It will return an `AnonymousAWSCredential`
if it fails to obtain a credential from the metadata API service.

> [!WARNING]
> This method will only work if: 
> - The discovery service is running inside an EC2 instance 
> - The EC2 instance metadata service is __NOT__ disabled (AWS_EC2_METADATA_DISABLED environment variable is __NOT__ set)
> - The IAM role of the instance is properly set, and
> - The metadata options of the instance is set to use the IMDSv2 metadata service.

#### Custom Credential Provider
To create a custom credential provider, you can extend the `Akka.Discovery.AwsApi.Ec2.Ec2CredentialProvider`
abstract class. You then create a custom configuration section that points to this class inside the HOCON
configuration file.

```
akka.discovery {
  aws-api-ec2-tag-based {
    credentials-provider = my-custom-credential-provider
    
    my-custom-credential-provider {
        class = "MyAkkaAssembly.CustomCredentialProvider, MyAkkaAssembly"    
    }
  }
}

```

## Notes
- You will need to make sure that the proper privileges are in place for the discovery implementation to access the Amazon EC2 API. The simplest way to do this is by creating a IAM role that, at a minimum, allows the DescribeInstances action. Attach this IAM role to the EC2 instances that need to access the discovery implementation. See the [docs for IAM Roles for Amazon EC2](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/iam-roles-for-amazon-ec2.html).
- In general, for the EC2 instances to “talk to each other” (necessary for forming a cluster), they need to be in the same security group and [the proper rules have to be set](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/security-group-rules-reference.html#sg-rules-other-instances).
- You can set additional filters (by instance type, region, other tags etc.) in your application.conf file, in the akka.discovery.aws-api-ec2-tag-based.filters key. The filters have to be key=value pairs separated by the semicolon character. For example:
```
akka.discovery.aws-api-ec2-tag-based {
    filters = "instance-type=m1.small;tag:purpose=production"
}
```
- By default, this module is configured for clusters with one Akka node per EC2 instance: it separates cluster members solely by their EC2 IP address. However, we can change the default configuration to indicate multiple ports per discovered EC2 IP, and achieve a setup with multiple Akka nodes per EC2 instance.
```
akka.discovery.aws-api-ec2-tag-based {
    ports = [8557, 8558, 8559] # 3 Akka nodes per EC2 instance
}
```
  Note: this comes with the limitation that each EC2 instance has to have the same number of Akka nodes.
- You can change the default tag key from “service” to something else. This can be done via application.conf, by setting `akka.discovery.aws-api-ec2-tag-based.tag-key` to something else.
```
akka.discovery.aws-api-ec2-tag-based {
    tag-key = "akka-cluster"
}
```

### I'm getting an `AmazonEC2Exception: The request must contain the parameter AWSAccessKeyId`
Please consider using the `Ec2InstanceMetadataCredentialProvider` credential provider if your discovery
service lives inside an EC2 instance. Make sure that your instance is assigned to a proper role and
the Instance Metadata Service (IMDS) version is set to IMDSv2.

You can check to see if the metadata service is running by running curl inside your instance:
```
curl http://169.254.169.254
```
It should return a 200-OK if the metadata service is running.

https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/configuring-instance-metadata-service.html