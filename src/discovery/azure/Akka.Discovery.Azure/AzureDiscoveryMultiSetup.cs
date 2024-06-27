using System.Collections.Concurrent;
using Akka.Actor.Setup;

namespace Akka.Discovery.Azure;

internal sealed class AzureDiscoveryMultiSetup : Setup
{
    public ConcurrentDictionary<string, AzureDiscoverySetup> Setups { get; } = new();
}