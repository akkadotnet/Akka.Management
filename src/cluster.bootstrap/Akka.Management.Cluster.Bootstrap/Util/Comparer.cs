using System;
using System.Collections.Generic;
using System.Net;
using Akka.Discovery;
using static Akka.Discovery.ServiceDiscovery;

namespace Akka.Management.Cluster.Bootstrap.Util
{
    public class ResolvedTargetComparer : IComparer<ResolvedTarget>
    {
        public static readonly ResolvedTargetComparer Instance = new ResolvedTargetComparer();
        
        public int Compare(ResolvedTarget x, ResolvedTarget y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            var addressComparison = IPAddressComparer.Instance.Compare(x.Address, y.Address);
            if (addressComparison != 0) return addressComparison;
            var hostComparison = string.Compare(x.Host, y.Host, StringComparison.Ordinal);
            if (hostComparison != 0) return hostComparison;
            return Nullable.Compare(x.Port, y.Port);
        }
    }

    public class IPAddressComparer : IComparer<IPAddress>
    {
        public static readonly IPAddressComparer Instance = new IPAddressComparer();
        
        public int Compare(IPAddress x, IPAddress y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;

            var bytesX = x.GetAddressBytes();
            var bytesY = y.GetAddressBytes();
            if (bytesX.Length > bytesY.Length) return 1;
            if (bytesY.Length > bytesX.Length) return -1;
            for (var i = 0; i < bytesX.Length; i++)
            {
                if (bytesX[i] > bytesY[i]) return 1;
                if (bytesY[i] > bytesX[i]) return -1;
            }

            return 0;
        }
    }
}