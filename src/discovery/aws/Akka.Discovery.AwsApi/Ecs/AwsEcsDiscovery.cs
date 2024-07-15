// -----------------------------------------------------------------------
//  <copyright file="EcsDiscovery.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using Akka.Actor;
using Akka.Configuration;
using Akka.Util;

namespace Akka.Discovery.AwsApi.Ecs
{
    public class AwsEcsDiscovery: IExtension
    {
        internal const string DefaultPath = "aws-api-ecs";
        internal const string DefaultConfigPath = "akka.discovery." + DefaultPath;
        
        public static Either<string, IPAddress> GetContainerAddress()
        {
            var addresses = NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(@interface => @interface.GetIPProperties().UnicastAddresses)
                .Select(info => info.Address)
                .Where(ip => ip.IsSiteLocalAddress() && !ip.IsLoopbackAddress()).ToList();
            if (addresses.Count == 1)
                return new Right<string, IPAddress>(addresses[0]);
            return new Left<string, IPAddress>(
                $"Exactly one private address must be configured (found: [{string.Join(",", addresses)}])");
        }
        
        public static Configuration.Config DefaultConfiguration()
            => ConfigurationFactory.FromResource<AwsEcsDiscovery>("Akka.Discovery.AwsApi.reference.conf");
        
        public static AwsEcsDiscovery Get(ActorSystem system)
            => system.WithExtension<AwsEcsDiscovery, EcsDiscoveryProvider>();

        public readonly EcsServiceDiscoverySettings Settings;

        public AwsEcsDiscovery(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());
            Settings = EcsServiceDiscoverySettings.Create(system);

            var setup = system.Settings.Setup.Get<EcsServiceDiscoverySetup>();
            if (setup.HasValue)
                Settings = setup.Value.Apply(Settings);
        }

    }
    
    public class EcsDiscoveryProvider : ExtensionIdProvider<AwsEcsDiscovery>
    {
        public override AwsEcsDiscovery CreateExtension(ExtendedActorSystem system)
            => new AwsEcsDiscovery(system);
    }
    
}