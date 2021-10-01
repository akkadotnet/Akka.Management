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