#AWS EC2 Tag-Based Discovery
This module can be used as a discovery method for an AWS EC2 based cluster.

You can use tags to simply mark the instances that belong to the same cluster. 
Use a tag that has “service” as the key and set the value equal to the name of your service.

Note that this implementation is adequate for users running service clusters on vanilla EC2 instances. 
These instances can be created and tagged manually, or created via an auto-scaling group (ASG). 
If they are created via an ASG, they can be tagged automatically on creation. 
Simply add the tag to the auto-scaling group configuration and ensure the “Tag New Instances” option is checked.

To use `Akka.Discovery.AwsApi` in your project, you must also include `Akka.Discovery` in your project nuget package dependency.
You will also need to include these HOCON settings in your HOCON configuration:
```properties
akka {
    discovery {
        method = aws-api-ec2-tag-based
        aws-api-ec2-tag-based {
            # Fully qualified class name of a class that extends Amazon.EC2.AmazonEC2Config with either 
            # a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
            client-config = ""

            class = "Akka.Discovery.AwsApi.Ec2.Ec2TagBasedServiceDiscovery, Akka.Discovery.AwsApi"

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
        }
    }
}
```

## Notes
- You will need to make sure that the proper privileges are in place for the discovery implementation to access the Amazon EC2 API. The simplest way to do this is by creating a IAM role that, at a minimum, allows the DescribeInstances action. Attach this IAM role to the EC2 instances that need to access the discovery implementation. See the [docs for IAM Roles for Amazon EC2](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/iam-roles-for-amazon-ec2.html).
- In general, for the EC2 instances to “talk to each other” (necessary for forming a cluster), they need to be in the same security group and [the proper rules have to be set](https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/security-group-rules-reference.html#sg-rules-other-instances).
- You can set additional filters (by instance type, region, other tags etc.) in your application.conf file, in the akka.discovery.aws-api-ec2-tag-based.filters key. The filters have to be key=value pairs separated by the semicolon character. For example:
```properties
akka.discovery.aws-api-ec2-tag-based {
    filters = "instance-type=m1.small;tag:purpose=production"
}
```
- By default, this module is configured for clusters with one Akka node per EC2 instance: it separates cluster members solely by their EC2 IP address. However, we can change the default configuration to indicate multiple ports per discovered EC2 IP, and achieve a setup with multiple Akka nodes per EC2 instance.
```properties
akka.discovery.aws-api-ec2-tag-based {
    ports = [8557, 8558, 8559] # 3 Akka nodes per EC2 instance
}
```
    Note: this comes with the limitation that each EC2 instance has to have the same number of Akka nodes.
- You can change the default tag key from “service” to something else. This can be done via application.conf, by setting `akka.discovery.aws-api-ec2-tag-based.tag-key` to something else.
```properties
akka.discovery.aws-api-ec2-tag-based {
    tag-key = "akka-cluster"
}
```
