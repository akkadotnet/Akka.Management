using Akka.Actor;
using Akka.Configuration;

namespace Akka.Discovery.Azure;

public class AzureDiscovery: IExtension
{
    public static Configuration.Config DefaultConfiguration()
        => ConfigurationFactory.FromResource<AzureDiscovery>("Akka.Discovery.Azure.reference.conf");
        
    public static AzureDiscovery Get(ActorSystem system)
        => system.WithExtension<AzureDiscovery, AzureDiscoveryProvider>();

    public readonly AzureDiscoverySettings Settings;

    public AzureDiscovery(ExtendedActorSystem system)
    {
        system.Settings.InjectTopLevelFallback(DefaultConfiguration());
        Settings = AzureDiscoverySettings.Create(system);

        var setup = system.Settings.Setup.Get<AzureDiscoverySetup>();
        if (setup.HasValue)
            Settings = setup.Value.Apply(Settings);
    }
}

public class AzureDiscoveryProvider : ExtensionIdProvider<AzureDiscovery>
{
    public override AzureDiscovery CreateExtension(ExtendedActorSystem system)
        => new AzureDiscovery(system);
}
