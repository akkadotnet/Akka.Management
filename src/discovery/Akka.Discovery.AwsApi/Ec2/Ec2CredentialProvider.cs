using System.Linq;
using Akka.Actor;
using Akka.Event;
using Amazon.Runtime;
using Amazon.Util;

namespace Akka.Discovery.AwsApi.Ec2
{
    public abstract class Ec2CredentialProvider
    {
        public abstract AWSCredentials ClientCredentials { get; }
    }

    public sealed class AnonymousEc2CredentialProvider : Ec2CredentialProvider
    {
        public override AWSCredentials ClientCredentials { get; } = new AnonymousAWSCredentials();
    }

    public sealed class Ec2InstanceMetadataCredentialProvider : Ec2CredentialProvider
    {
        private readonly ILoggingAdapter _log;
        private readonly string _role;

        public Ec2InstanceMetadataCredentialProvider(ActorSystem system)
        {
            _log = Logging.GetLogger(system, typeof(Ec2InstanceMetadataCredentialProvider));
            _role = system.Settings.Config.GetString(
                "akka.discovery.aws-api-ec2-tag-based.instance-metadata-provider.role");
        }

        public override AWSCredentials ClientCredentials
        {
            get
            {
                if (!EC2InstanceMetadata.IsIMDSEnabled)
                {
                    _log.Warning("Could not obtain EC2 client credentials because instance metadata is disabled. Using anonymous credentials instead.");
                    return new AnonymousAWSCredentials();
                }

                var credentials = EC2InstanceMetadata.IAMSecurityCredentials;
                if (credentials == null)
                {
                    _log.Warning("Could not obtain EC2 client credentials, call to metadata API failed. Using anonymous credentials instead.");
                    return new AnonymousAWSCredentials();
                }

                if (string.IsNullOrWhiteSpace(_role))
                {
                    foreach (var cred in credentials.Values)
                    {
                        if(cred.Code != "Failed")
                            return new SessionAWSCredentials(cred.AccessKeyId, cred.SecretAccessKey, cred.Token);
                    }
                    _log.Warning($"Could not obtain EC2 client credentials, no viable credentials are found. Using anonymous credentials instead.");
                    return new AnonymousAWSCredentials();
                }
                
                if (!credentials.TryGetValue(_role, out var credential))
                {
                    _log.Warning($"Could not obtain EC2 client credentials, no role called [{_role}] found. Using anonymous credentials instead. " +
                                 $"Available roles: [{string.Join(", ", credentials.Select(kvp => kvp.Key))}]");
                    return new AnonymousAWSCredentials();
                }

                if (credential.Code == "Failed")
                {
                    _log.Warning($"Could not obtain EC2 client credentials, failed to retrieve credentials for role [{_role}]. Using anonymous credentials instead.");
                    return new AnonymousAWSCredentials();
                }
                
                return new SessionAWSCredentials(credential.AccessKeyId, credential.SecretAccessKey, credential.Token);
            }
        }
    }
}