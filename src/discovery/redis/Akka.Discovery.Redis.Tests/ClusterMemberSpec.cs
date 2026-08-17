// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Linq;
using FluentAssertions;
using Google.Protobuf;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    /// <summary>
    /// Redis entries are stored as protobuf bytes (<c>ClusterMemberProto</c>), matching the
    /// extend-only contract used by Akka.Discovery.Azure. These specs pin the wire-format round-trip
    /// and its schema-evolution tolerance (unknown fields ignored, absent fields default), which is
    /// the compatibility guarantee during rolling upgrades across plugin versions.
    /// </summary>
    public class ClusterMemberSpec
    {
        private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Should_round_trip_through_protobuf()
        {
            var member = ClusterMember.CreateEntity("my-service", "10.0.0.5", 8558);

            var restored = ClusterMember.FromBytes(member.ToBytes());

            restored.ServiceName.Should().Be(member.ServiceName);
            restored.Host.Should().Be(member.Host);
            restored.Port.Should().Be(member.Port);
            restored.Created.Should().Be(member.Created);
            restored.LastUpdate.Should().Be(member.LastUpdate);
        }

        [Fact]
        public void Should_ignore_unknown_fields_from_a_newer_version()
        {
            // Simulate a newer producer that added field #6. Extend-only compatibility requires an
            // older reader to ignore it rather than throw.
            var member = ClusterMember.CreateEntity("svc", "host", 42);
            // field 6, wire type 0 (varint) => tag 0x30, value 0x01
            var withUnknownField = member.ToBytes().Concat(new byte[] { 0x30, 0x01 }).ToArray();

            var act = () => ClusterMember.FromBytes(withUnknownField);

            act.Should().NotThrow();
            var restored = act();
            restored.ServiceName.Should().Be("svc");
            restored.Host.Should().Be("host");
            restored.Port.Should().Be(42);
        }

        [Fact]
        public void Should_default_absent_timestamps_to_epoch()
        {
            // A payload written before created/last_update existed must read back as maximally stale,
            // not throw.
            var proto = new ClusterMemberProto { ServiceName = "svc", Host = "host", Port = 42 };

            var restored = ClusterMember.FromBytes(proto.ToByteArray());

            restored.Created.Should().Be(Epoch);
            restored.LastUpdate.Should().Be(Epoch);
        }

        [Fact]
        public void CreateKey_should_be_stable_and_namespaced()
        {
            var key = ClusterMember.CreateKey("akka:discovery", "svc", "10.0.0.5", 8558);
            key.Should().Be("akka:discovery:svc:10.0.0.5:8558");
        }
    }
}
