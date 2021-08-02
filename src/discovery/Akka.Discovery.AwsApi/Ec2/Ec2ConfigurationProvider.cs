using Amazon.EC2;
using Amazon.Runtime;

namespace Akka.Discovery.AwsApi.Ec2
{
    public abstract class Ec2ConfigurationProvider
    {
        public abstract AmazonEC2Config ClientConfiguration { get; }
        public abstract AWSCredentials ClientCredentials { get; }
    }

    public sealed class DefaultEc2ConfigurationProvider : Ec2ConfigurationProvider
    {
        public override AmazonEC2Config ClientConfiguration { get; } = new AmazonEC2Config();
        public override AWSCredentials ClientCredentials { get; } = new AnonymousAWSCredentials();
    }
}