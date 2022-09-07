//-----------------------------------------------------------------------
// <copyright file="UtilsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Net;
using Akka.Discovery.AwsApi.Ecs;
using Amazon.ECS.Model;
using FluentAssertions;
using Xunit;

namespace Akka.Discovery.AwsApi.Tests
{
    public class UtilsSpec
    {
        [Fact(DisplayName = "ChunkBy with items less than chunk count should work")]
        public void ChunkBy10()
        {
            var list = Enumerable.Range(0, 10).Select(i => i.ToString());
            var chunked = list.ChunkBy(20).ToList();
            chunked.Count.Should().Be(1);
            chunked[0].Should().BeEquivalentTo(Enumerable.Range(0, 10).Select(i => i.ToString()));
        }
        
        [Fact(DisplayName = "ChunkBy with items more than chunk count should work")]
        public void ChunkBy50()
        {
            var list = Enumerable.Range(0, 50).Select(i => i.ToString());
            var chunked = list.ChunkBy(20).ToList();
            chunked.Count.Should().Be(3);
            chunked[0].Should().BeEquivalentTo(Enumerable.Range(0, 20).Select(i => i.ToString()));
            chunked[1].Should().BeEquivalentTo(Enumerable.Range(20, 20).Select(i => i.ToString()));
            chunked[2].Should().BeEquivalentTo(Enumerable.Range(40, 10).Select(i => i.ToString()));
        }
        
        [Theory(DisplayName = "Diff should return appropriate HashSet")]
        [MemberData(nameof(TagDataSource))]
        public void DiffTest(List<Tag> listA, List<Tag> listB, List<Tag> listDiff)
        {
            var result = listA.Diff(listB, EcsServiceDiscovery.TagComparer);
            result.Count.Should().Be(listDiff.Count);
            foreach (var tag in listDiff)
            {
                result.Should().Contain(tag);
            }
        }
        
        [Theory(DisplayName = "IsSame should return appropriate result")]
        [MemberData(nameof(TagDataSource))]
        public void IsSameTest(List<Tag> listA, List<Tag> listB, List<Tag> listDiff)
        {
            listA.IsSame(listB, EcsServiceDiscovery.TagComparer).Should().Be(listDiff.Count == 0);
        }
        
        public static IEnumerable<object[]> TagDataSource()
        {
            var data = new object[][]
            {
                new object[]
                {
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                        new Tag{Key = "c", Value = "d"},
                    }, 
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                        new Tag{Key = "c", Value = "d"},
                    },
                    new List<Tag>()
                },
                new object[]
                {
                    new List<Tag>(), 
                    new List<Tag>(),
                    new List<Tag>()
                },
                new object[]
                {
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                        new Tag{Key = "c", Value = "d"},
                    }, 
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                        new Tag{Key = "d", Value = "e"},
                    },
                    new List<Tag>
                    {
                        new Tag{Key = "c", Value = "d"},
                        new Tag{Key = "d", Value = "e"},
                    }
                },
                new object[]
                {
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                    }, 
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                        new Tag{Key = "d", Value = "e"},
                    },
                    new List<Tag>
                    {
                        new Tag{Key = "d", Value = "e"},
                    }
                },
                new object[]
                {
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                        new Tag{Key = "c", Value = "d"},
                    }, 
                    new List<Tag>
                    {
                        new Tag{Key = "a", Value = "b"},
                        new Tag{Key = "b", Value = "c"},
                    },
                    new List<Tag>
                    {
                        new Tag{Key = "c", Value = "d"},
                    }
                },
            };

            foreach (var ip in data)
            {
                yield return ip;
            }
        }
        
        
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