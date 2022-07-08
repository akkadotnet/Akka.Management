// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberEntitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Discovery.Azure.Model;
using Akka.Discovery.Azure.Tests.Utils;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

// If anything throws InvalidOperationException, then the test failed anyway.
// ReSharper disable PossibleInvalidOperationException

namespace Akka.Discovery.Azure.Tests
{
    public class ClusterMemberEntitySpec
    {
        private const string ServiceName = "FakeService";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int Port = 12345;

        [Fact(DisplayName = "Should be able to create TableEntity")]
        public void ClusterMemberEntityTableEntityCreation()
        {
            var entity = ClusterMember.CreateEntity(ServiceName, _address, Port);

            var proto = ClusterMemberProto.Parser.ParseFrom(entity.GetBinary(ClusterMember.PayloadName));
            
            var created = proto.Created.ToDateTime();
            created.Should().BeApproximately(DateTime.UtcNow, 200.Milliseconds());
            entity.GetInt64(ClusterMember.LastUpdateName).Should().Be(created.Ticks);
            
            IPAddress.Parse(proto.Address).Should().Be(_address);
            proto.Port.Should().Be(Port);
            proto.Host.Should().Be(Dns.GetHostName());
            
            
            entity.PartitionKey.Should().Be(ServiceName);
            entity.RowKey.Should().NotBeNullOrWhiteSpace();
            entity.RowKey.Should().Be(ClusterMember.CreateRowKey(_address, Port));
        }

        [Fact(DisplayName = "Should be able to create ClusterMemberEntity from TableEntity")]
        public void ClusterMemberEntityCreation()
        {
            var entity = ClusterMember.FromEntity(ClusterMember.CreateEntity(ServiceName, _address, Port));
            
            entity.Created.Should().BeApproximately(DateTime.UtcNow, 200.Milliseconds());
            entity.Created.Should().Be(entity.LastUpdate);
            
            entity.PartitionKey.Should().Be(ServiceName);
            entity.RowKey.Should().NotBeNullOrWhiteSpace();
            entity.RowKey.Should().Be(ClusterMember.CreateRowKey(_address, Port));
            entity.Host.Should().Be(Dns.GetHostName());
            entity.Address.Should().Be(_address);
            entity.Port.Should().Be(Port);
        }

        [Fact(DisplayName = "Should create and parse RowKey properly")]
        public void ClusterMemberEntityRowKeyTest()
        {
            var rowKey = ClusterMember.CreateRowKey(_address, Port);
            var (address, port) = ClusterMember.ParseRowKey(rowKey);

            address.Should().Be(_address);
            port.Should().Be(Port);
        }
    }
}