//-----------------------------------------------------------------------
// <copyright file="Utils.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
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

        /// <summary>
        /// Emulates Scala `list.diff()`
        /// 
        /// * Removes members of left that matches a member of right once.
        /// * Does not remove duplicates.
        /// * Preserving order
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <typeparam name="T">Generic parameter type, must implement <see cref="IEquatable{T}"/></typeparam>
        /// <returns>
        /// A <see cref="List{T}"/> that is a subset of <paramref name="left"/> with members that matches
        /// <paramref name="right"/> once.
        /// </returns>
        /// <example>
        /// [1, 2, 2, 3, 4, 5, 5].Diff([2, 3, 4, 6]) == [1, 2, 5, 5]; // duplicate 2 and 5, 2 matches only once
        /// </example>
        internal static List<T> Diff<T>(this IEnumerable<T> left, IEnumerable<T> right) where T : IEquatable<T>
        {
            var l = new List<T>(left);
            foreach (var v in right)
            {
                var index = l.IndexOf(v);
                if(index != -1)
                    l.RemoveAt(index);
            }
            
            return l;
        }
    }

    public sealed class EcsTagComparer : IEqualityComparer<Tag>
    {
        public bool Equals(Tag? x, Tag? y)
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