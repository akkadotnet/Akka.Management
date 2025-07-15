using System;
using Akka.IO;

namespace Akka.Discovery.Dns.Internal;

public class AsyncDnsProvider : IDnsProvider
{
    /// <summary>
    /// TBD
    /// </summary>
    public DnsBase Cache { get; } = new AsyncDnsCache();

    /// <summary>
    /// TBD
    /// </summary>
    public Type ActorClass => typeof (DnsClient);

    /// <summary>
    /// TBD
    /// </summary>
    public Type ManagerClass => typeof (AsyncDnsManager);
}