using System.Collections.Immutable;
using Akka.Actor.Setup;

namespace Akka.Discovery.Azure;

internal sealed class AzureDiscoveryMultiSetup : Setup
{
    public AzureDiscoveryMultiSetup()
    {
        Setups = ImmutableDictionary<string, AzureDiscoverySetup>.Empty;
    }
    
    public ImmutableDictionary<string, AzureDiscoverySetup> Setups { get; private set; }

    public void Add(string path, AzureDiscoverySetup setup)
    {
        Setups = Setups.SetItem(path, setup);
    }
}