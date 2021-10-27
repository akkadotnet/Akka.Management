//-----------------------------------------------------------------------
// <copyright file="KubernetesDiscovery.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;
using Akka.Configuration;

namespace Akka.Discovery.KubernetesApi
{
    public class KubernetesDiscovery : IExtension
    {
        public static Configuration.Config DefaultConfiguration()
            => ConfigurationFactory.FromResource<KubernetesDiscovery>("Akka.Discovery.KubernetesApi.reference.conf");
        
        public static KubernetesDiscovery Get(ActorSystem system)
            => system.WithExtension<KubernetesDiscovery, KubernetesDiscoveryProvider>();

        public readonly KubernetesDiscoverySettings Settings;

        public KubernetesDiscovery(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());
            Settings = new KubernetesDiscoverySettings(system);
        }
    }

    public class KubernetesDiscoveryProvider : ExtensionIdProvider<KubernetesDiscovery>
    {
        public override KubernetesDiscovery CreateExtension(ExtendedActorSystem system)
            => new KubernetesDiscovery(system);
    }
}