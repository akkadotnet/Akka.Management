// -----------------------------------------------------------------------
//  <copyright file="AwsEc2Discovery.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Configuration;

namespace Akka.Discovery.AwsApi.Ec2
{
    public class AwsEc2Discovery : IExtension
    {
        internal const string DefaultPath = "aws-api-ec2-tag-based";
        internal const string DefaultConfigPath = "akka.discovery." + DefaultPath;
        
        public static Configuration.Config DefaultConfiguration()
            => ConfigurationFactory.FromResource<AwsEc2Discovery>("Akka.Discovery.AwsApi.reference.conf");
        
        public static AwsEc2Discovery Get(ActorSystem system)
            => system.WithExtension<AwsEc2Discovery, AwsEc2DiscoveryProvider>();

        public readonly Ec2ServiceDiscoverySettings Settings;

        public AwsEc2Discovery(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());
            Settings = Ec2ServiceDiscoverySettings.Create(system);

            var setup = system.Settings.Setup.Get<Ec2ServiceDiscoverySetup>();
            if (setup.HasValue)
                Settings = setup.Value.Apply(Settings);
        }
        
    }
    
    public class AwsEc2DiscoveryProvider : ExtensionIdProvider<AwsEc2Discovery>
    {
        public override AwsEc2Discovery CreateExtension(ExtendedActorSystem system)
            => new AwsEc2Discovery(system);
    }
    
}