using System;
using Akka.IO;

namespace Akka.Discovery.Dns.Internal;

/// <summary>
/// This interface is used by DnsServiceDiscovery to determine if SRV or A/AAAA lookup should be performed
/// </summary>
public interface IDnsProviderWithSrvLookup : IDnsProvider;
public class AsyncDnsProvider : IDnsProviderWithSrvLookup
{
    /// <summary>
    /// TBD
    /// </summary>
    public DnsBase Cache { get; } = new AsyncDnsCache();

    /// <summary>
    /// TBD
    /// </summary>
    public virtual Type ActorClass => typeof (AsyncDnsClient);

    /// <summary>
    /// TBD
    /// </summary>
    public virtual Type ManagerClass => typeof (AsyncDnsManager);
}