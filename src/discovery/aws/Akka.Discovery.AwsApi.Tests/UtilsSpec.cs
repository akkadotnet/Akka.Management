//-----------------------------------------------------------------------
// <copyright file="UtilsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Xunit;

namespace Akka.Discovery.AwsApi.Tests
{
    public class UtilsSpec
    {
        [Theory(DisplayName = "IPAddress.IsLocalhostAddress() extension method should work properly")]
        [MemberData(nameof(LocalhostIpAddressDataSource))]
        public void IsLocalhostAddress(IPAddress address, bool isLocal)
        {
            address.IsLoopbackAddress().Should().Be(isLocal);
        }

        [Theory(DisplayName = "IPAddress.IsSiteLocalAddress() extension method should work properly")]
        [MemberData(nameof(SiteLocalIpAddressDataSource))]
        public void IsSiteLocalAddress(IPAddress address, bool isLocal)
        {
            address.IsSiteLocalAddress().Should().Be(isLocal);
        }

        public static IEnumerable<object[]> LocalhostIpAddressDataSource()
        {
            var data = new object[][]
            {
                new object[]{IPAddress.Parse("127.0.0.1"), true},
                new object[]{IPAddress.Parse("127.0.128.1"), true},
                new object[]{IPAddress.Parse("127.122.9.99"), true},
                new object[]{IPAddress.Parse("10.0.0.1"), false},
                new object[]{IPAddress.Parse("129.10.240.5"), false},
                
                new object[]{IPAddress.Parse("::1"), true},
                new object[]{IPAddress.Parse("0:0:0:0:0:ffff:ff00:1"), false},
                new object[]{IPAddress.Parse("::100"), false},
                new object[]{IPAddress.Parse("1::1"), false},
                new object[]{IPAddress.Parse("127::1"), false},
            };

            foreach (var ip in data)
            {
                yield return ip;
            }
        }
        
        public static IEnumerable<object[]> SiteLocalIpAddressDataSource()
        {
            var data = new object[][]
            {
                new object[]{IPAddress.Parse("10.0.0.1"), true},
                new object[]{IPAddress.Parse("10.0.0.8"), true},
                new object[]{IPAddress.Parse("172.16.9.99"), true},
                new object[]{IPAddress.Parse("172.16.0.1"), true},
                new object[]{IPAddress.Parse("172.26.10.1"), true},
                new object[]{IPAddress.Parse("172.31.100.1"), true},
                new object[]{IPAddress.Parse("192.168.1.5"), true},
                new object[]{IPAddress.Parse("192.168.0.1"), true},
                new object[]{IPAddress.Parse("12.16.0.1"), false},
                new object[]{IPAddress.Parse("125.16.0.1"), false},
                new object[]{IPAddress.Parse("116.16.0.1"), false},
                
                new object[]{IPAddress.Parse("FEC0::1"), true},
                new object[]{IPAddress.Parse("FEC0:0:0:0:0:ffff:ff00:1"), true},
                new object[]{IPAddress.Parse("::100"), false},
                new object[]{IPAddress.Parse("1::1"), false},
                new object[]{IPAddress.Parse("127::1"), false},
            };

            foreach (var ip in data)
            {
                yield return ip;
            }
        }
        
    }
}