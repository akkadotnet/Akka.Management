//-----------------------------------------------------------------------
// <copyright file="KubernetesDiscovery.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
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

        [Obsolete("The Settings property is now deprecated. Since 1.5.26")]
        public readonly KubernetesDiscoverySettings Settings;

#pragma warning disable CS0618 // Type or member is obsolete
        public KubernetesDiscovery(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());
            Settings = KubernetesDiscoverySettings.Create(system);

            var setup = system.Settings.Setup.Get<KubernetesDiscoverySetup>();
            if (setup.HasValue)
                Settings = setup.Value.Apply(Settings);
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public class KubernetesDiscoveryProvider : ExtensionIdProvider<KubernetesDiscovery>
    {
        public override KubernetesDiscovery CreateExtension(ExtendedActorSystem system)
            => new KubernetesDiscovery(system);
    }
}