//-----------------------------------------------------------------------
// <copyright file="Utils.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Amazon.ECS.Model;

namespace Akka.Discovery.AwsApi
{
    public static class IpAddressExtensions
    {
        public static bool IsLoopbackAddress(this IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var test = (byte) 0;
                for(var i=0; i<15; i++)
                {
                    test |= bytes[i];
                }

                return test == 0x00 && bytes[15] == 0x01;
            }
            
            /* 127.x.x.x */
            return bytes[0] == 127;
        }
        
        public static bool IsSiteLocalAddress(this IPAddress address)
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return address.IsIPv6SiteLocal;
            var raw = address.GetAddressBytes();
            
            // refer to RFC 1918
            // 10/8 prefix
            // 172.16/12 prefix
            // 192.168/16 prefix
            return (raw[0] & 0xFF) == 10
                   || ((raw[0] & 0xFF) == 172 && (raw[1] & 0xF0) == 16)
                   || ((raw[0] & 0xFF) == 192 && (raw[1] & 0xFF) == 168);
        }
    }
    
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> ChunkBy<T>(this IEnumerable<T> list, int chunk)
            => list
                .Select((value, index) => new { value, index })
                .GroupBy(x => x.index / chunk)
                .Select(x => x.Select(v => v.value));

        public static bool IsSame<T>(this IEnumerable<T> left, IEnumerable<T> right, IEqualityComparer<T> comparer)
        {
            var l = new HashSet<T>(left, comparer);
            var r = new HashSet<T>(right, comparer);
            if (l.Count != r.Count)
                return false;
            var same = l.Where(tag => r.Contains(tag)).ToList();
            return same.Count == l.Count;
        }
        
        public static HashSet<T> Diff<T>(this IEnumerable<T> left, IEnumerable<T> right, IEqualityComparer<T> comparer)
        {
            var l = new HashSet<T>(left, comparer);
            var r = new HashSet<T>(right, comparer);
            var removed = l.Where(tag => r.Contains(tag)).ToList();

            foreach (var remove in removed)
            {
                l.Remove(remove);
                r.Remove(remove);
            }
            
            var result = new HashSet<T>(l, comparer);
            result.UnionWith(r);
            return result;
        }
    }

    public sealed class EcsTagComparer : IEqualityComparer<Tag>
    {
        public bool Equals(Tag x, Tag y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Key == y.Key && x.Value == y.Value;
        }

        public int GetHashCode(Tag obj)
        {
            unchecked
            {
                return ((obj.Key != null ? obj.Key.GetHashCode() : 0) * 397) ^ (obj.Value != null ? obj.Value.GetHashCode() : 0);
            }
        }
    }
}