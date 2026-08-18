// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberEntitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Discovery.Azure.Model;
using Akka.Discovery.Azure.Tests.Utils;
using Xunit;

// If anything throws InvalidOperationException, then the test failed anyway.
// ReSharper disable PossibleInvalidOperationException

namespace Akka.Discovery.Azure.Tests
{
    public class ClusterMemberEntitySpec
    {
        private const string ServiceName = "FakeService";
        private const string Host = "fake.com";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int Port = 12345;

        [Fact(DisplayName = "Should be able to create TableEntity")]
        public void ClusterMemberEntityTableEntityCreation()
        {
            var entity = ClusterMember.CreateEntity(ServiceName, Host, _address, Port);

            var proto = ClusterMemberProto.Parser.ParseFrom(entity.GetBinary(ClusterMember.PayloadName));
            
            var created = proto.Created.ToDateTime();
            created.BeApproximately(DateTime.UtcNow, TimeSpan.FromMilliseconds(200));
            Assert.Equal(created.Ticks, entity.GetInt64(ClusterMember.LastUpdateName));
            
            Assert.Equal(_address, IPAddress.Parse(proto.Address));
            Assert.Equal(Port, proto.Port);
            Assert.Equal(Host, proto.Host);
            
            
            Assert.Equal(ServiceName, entity.PartitionKey);
            Assert.False(string.IsNullOrWhiteSpace(entity.RowKey));
            Assert.Equal(ClusterMember.CreateRowKey(Host, _address, Port), entity.RowKey);
        }

        [Fact(DisplayName = "Should be able to create ClusterMemberEntity from TableEntity")]
        public void ClusterMemberEntityCreation()
        {
            var entity = ClusterMember.FromEntity(ClusterMember.CreateEntity(ServiceName, Host, _address, Port));
            
            entity.Created.BeApproximately(DateTime.UtcNow, TimeSpan.FromMilliseconds(200));
            Assert.Equal(entity.LastUpdate, entity.Created);
            
            Assert.Equal(ServiceName, entity.PartitionKey);
            Assert.False(string.IsNullOrWhiteSpace(entity.RowKey));
            Assert.Equal(ClusterMember.CreateRowKey(Host, _address, Port), entity.RowKey);
            Assert.Equal(Host, entity.Host);
            Assert.Equal(_address, entity.Address);
            Assert.Equal(Port, entity.Port);
        }

        [Fact(DisplayName = "Should create and parse RowKey properly")]
        public void ClusterMemberEntityRowKeyTest()
        {
            var rowKey = ClusterMember.CreateRowKey(Host, _address, Port);
            var (host, address, port) = ClusterMember.ParseRowKey(rowKey);

            Assert.Equal(Host, host);
            Assert.Equal(_address, address);
            Assert.Equal(Port, port);
        }
    }
}