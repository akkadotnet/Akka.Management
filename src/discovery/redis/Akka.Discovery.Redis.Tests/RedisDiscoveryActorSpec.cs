// -----------------------------------------------------------------------
//  <copyright file="RedisDiscoveryActorSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;
using Akka.Discovery.Redis.Actors;
using FluentAssertions;
using FluentAssertions.Extensions;
using StackExchange.Redis;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    [Collection(nameof(RedisSpecs))]
    [Trait("Category", "Integration")]
    public class RedisDiscoveryActorSpec : TestKit.Xunit.TestKit, IAsyncLifetime
    {
        private const string ServiceName = nameof(ServiceName);
        private const string Host = "10.1.2.3";
        private const int SelfPort = 8558;

        // Unique per test instance so the tests in this class never share a Redis key. Without this,
        // a prior test's in-flight heartbeat write (a StringSet that completes *after* the guardian
        // stops, i.e. after RedisFixture.ClearAsync has already run for the next test) can re-create
        // the very key StopDiscoveryShouldRemoveSelf asserts is gone. xUnit builds a fresh instance
        // per test method, so each test gets its own prefix.
        private readonly string _keyPrefix = $"akka:discovery:{Guid.NewGuid():N}";

        private readonly RedisFixture _fixture;
        private RedisDiscoverySettings _settings = null!;

        public RedisDiscoveryActorSpec(ITestOutputHelper helper, RedisFixture fixture)
            : base("akka.loglevel = DEBUG", nameof(RedisDiscoveryActorSpec), helper)
        {
            _fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            _fixture.EnsureAvailable();
            await _fixture.ClearAsync();

            _settings = RedisDiscoverySettings.Empty
                .WithServiceName(ServiceName)
                .WithHostName(Host)
                .WithPort(SelfPort)
                .WithKeyPrefix(_keyPrefix)
                .WithConnectionString(_fixture.ConnectionString)
                .WithTtlHeartbeatInterval(1.Seconds());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        [Fact(DisplayName = "Guardian should register self and return it on lookup")]
        public async Task GuardianShouldRegisterAndResolveSelf()
        {
            var guardian = Sys.ActorOf(RedisDiscoveryGuardian.Props(_settings));

            await AwaitAssertAsync(async () =>
            {
                var members = await guardian.Ask<ImmutableList<ClusterMember>>(new Lookup(ServiceName), 3.Seconds());
                members.Count.Should().Be(1);
                members[0].Host.Should().Be(Host);
                members[0].Port.Should().Be(SelfPort);
            }, 20.Seconds(), 500.Milliseconds());
        }

        [Fact(DisplayName = "Guardian should return empty on a service-name mismatch")]
        public async Task GuardianShouldRejectMismatchedServiceName()
        {
            var guardian = Sys.ActorOf(RedisDiscoveryGuardian.Props(_settings));

            // wait until initialized (self resolvable)
            await AwaitAssertAsync(async () =>
            {
                var members = await guardian.Ask<ImmutableList<ClusterMember>>(new Lookup(ServiceName), 3.Seconds());
                members.Count.Should().Be(1);
            }, 20.Seconds(), 500.Milliseconds());

            var mismatch = await guardian.Ask<ImmutableList<ClusterMember>>(new Lookup("some-other-service"), 3.Seconds());
            mismatch.Should().BeEmpty();
        }

        [Fact(DisplayName = "Heartbeat child should keep refreshing the self entry")]
        public async Task HeartbeatShouldRefreshEntry()
        {
            Sys.ActorOf(RedisDiscoveryGuardian.Props(_settings));

            await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
            var database = connection.GetDatabase();
            var key = ClusterMember.CreateKey(_keyPrefix, ServiceName, Host, SelfPort);

            async Task<DateTime> ReadLastUpdate()
            {
                var value = await database.StringGetAsync(key);
                value.HasValue.Should().BeTrue();
                return ClusterMember.FromBytes((byte[])value!).LastUpdate;
            }

            // Wait until the self entry is registered, capture its first LastUpdate.
            DateTime first = default;
            await AwaitAssertAsync(async () =>
            {
                (await database.KeyExistsAsync(key)).Should().BeTrue();
                first = await ReadLastUpdate();
            }, 20.Seconds(), 500.Milliseconds());

            // The 1s heartbeat must advance LastUpdate over time.
            await AwaitAssertAsync(async () =>
            {
                var current = await ReadLastUpdate();
                current.Should().BeAfter(first);
            }, 10.Seconds(), 500.Milliseconds());
        }

        [Fact(DisplayName = "StopDiscovery should remove the self entry from Redis")]
        public async Task StopDiscoveryShouldRemoveSelf()
        {
            // Use a long heartbeat so this guardian's *own* heartbeat cannot have a write in flight that
            // lands after RemoveSelf and re-creates the entry during shutdown (self is registered at init,
            // not by the heartbeat). Cross-test collisions are handled separately by the per-test _keyPrefix.
            var settings = _settings.WithTtlHeartbeatInterval(TimeSpan.FromMinutes(1));
            var guardian = Sys.ActorOf(RedisDiscoveryGuardian.Props(settings));

            await AwaitAssertAsync(async () =>
            {
                var members = await guardian.Ask<ImmutableList<ClusterMember>>(new Lookup(ServiceName), 3.Seconds());
                members.Count.Should().Be(1);
            }, 20.Seconds(), 500.Milliseconds());

            await guardian.Ask<Done>(StopDiscovery.Instance, 5.Seconds());

            await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
            var key = ClusterMember.CreateKey(_keyPrefix, ServiceName, Host, SelfPort);
            (await connection.GetDatabase().StringGetAsync(key)).HasValue.Should().BeFalse();
        }
    }
}
