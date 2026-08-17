// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    /// <summary>
    /// The Redis entries are stored as camelCase JSON. These specs pin the wire format and its
    /// tolerance of missing/unknown fields, which is the compatibility contract during rolling
    /// upgrades across plugin versions.
    /// </summary>
    public class ClusterMemberSpec
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        [Fact]
        public void Should_round_trip_through_json()
        {
            var member = ClusterMember.CreateEntity("my-service", "10.0.0.5", 8558);

            var json = JsonSerializer.Serialize(member, JsonOptions);
            var restored = JsonSerializer.Deserialize<ClusterMember>(json, JsonOptions)!;

            restored.ServiceName.Should().Be(member.ServiceName);
            restored.Host.Should().Be(member.Host);
            restored.Port.Should().Be(member.Port);
            restored.Created.Should().Be(member.Created);
            restored.LastUpdate.Should().Be(member.LastUpdate);
        }

        [Fact]
        public void Should_serialize_using_camel_case_property_names()
        {
            var member = ClusterMember.CreateEntity("svc", "host", 1234);
            var json = JsonSerializer.Serialize(member, JsonOptions);

            json.Should().Contain("\"serviceName\":");
            json.Should().Contain("\"lastUpdate\":");
        }

        [Fact]
        public void Should_tolerate_missing_fields()
        {
            // An older/newer producer that omits timestamps must not blow up the reader.
            const string json = "{\"serviceName\":\"svc\",\"host\":\"host\",\"port\":42}";

            var restored = JsonSerializer.Deserialize<ClusterMember>(json, JsonOptions)!;

            restored.ServiceName.Should().Be("svc");
            restored.Host.Should().Be("host");
            restored.Port.Should().Be(42);
            restored.Created.Should().Be(default);
            restored.LastUpdate.Should().Be(default);
        }

        [Fact]
        public void Should_ignore_unknown_fields()
        {
            const string json = "{\"serviceName\":\"svc\",\"host\":\"host\",\"port\":42," +
                                 "\"created\":\"2026-01-01T00:00:00Z\",\"lastUpdate\":\"2026-01-01T00:00:00Z\"," +
                                 "\"somethingNew\":\"ignored\"}";

            var act = () => JsonSerializer.Deserialize<ClusterMember>(json, JsonOptions);

            act.Should().NotThrow();
            act()!.ServiceName.Should().Be("svc");
        }

        [Fact]
        public void CreateKey_should_be_stable_and_namespaced()
        {
            var key = ClusterMember.CreateKey("akka:discovery", "svc", "10.0.0.5", 8558);
            key.Should().Be("akka:discovery:svc:10.0.0.5:8558");
        }
    }
}
