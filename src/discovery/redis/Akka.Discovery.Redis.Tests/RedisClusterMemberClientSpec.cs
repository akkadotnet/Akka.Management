// -----------------------------------------------------------------------
//  <copyright file="RedisClusterMemberClientSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Threading.Tasks;
using Akka.Event;
using FluentAssertions;
using FluentAssertions.Extensions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using StackExchange.Redis;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    [Collection(nameof(RedisSpecs))]
    [Trait("Category", "Integration")]
    public class RedisClusterMemberClientSpec : TestKit.Xunit.TestKit, IAsyncLifetime
    {
        private const string ServiceName = nameof(ServiceName);
        private const string Host = "10.0.0.1";
        private const int SelfPort = 8558;
        private const string KeyPrefix = "akka:discovery";

        private readonly RedisFixture _fixture;
        private readonly RedisDiscoverySettings _settings;
        private IConnectionMultiplexer _connection = null!;
        private IDatabase _database = null!;
        private ClusterMemberRedisClient _client = null!;

        public RedisClusterMemberClientSpec(ITestOutputHelper helper, RedisFixture fixture)
            : base("akka.loglevel = DEBUG", nameof(RedisClusterMemberClientSpec), helper)
        {
            _fixture = fixture;
            _settings = RedisDiscoverySettings.Empty
                .WithServiceName(ServiceName)
                .WithHostName(Host)
                .WithPort(SelfPort)
                .WithKeyPrefix(KeyPrefix);
        }

        public async ValueTask InitializeAsync()
        {
            _fixture.EnsureAvailable();
            await _fixture.ClearAsync();

            _connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
            _database = _connection.GetDatabase();
            _client = new ClusterMemberRedisClient(_connection, _settings, Logging.GetLogger(Sys, nameof(ClusterMemberRedisClient)));
        }

        public async ValueTask DisposeAsync()
        {
            if (_connection is not null)
                await _connection.DisposeAsync();
        }

        private Task SeedAsync(string host, int port, DateTime lastUpdate)
        {
            var ts = Timestamp.FromDateTime(DateTime.SpecifyKind(lastUpdate, DateTimeKind.Utc));
            var proto = new ClusterMemberProto
            {
                ServiceName = ServiceName, Host = host, Port = port, Created = ts, LastUpdate = ts
            };
            var key = ClusterMember.CreateKey(KeyPrefix, ServiceName, host, port);
            return _database.StringSetAsync(key, proto.ToByteArray());
        }

        [Fact(DisplayName = "GetOrCreateAsync should insert a new entry")]
        public async Task GetOrCreateShouldInsert()
        {
            var entity = await _client.GetOrCreateAsync();

            entity.ServiceName.Should().Be(ServiceName);
            entity.Host.Should().Be(Host);
            entity.Port.Should().Be(SelfPort);

            var raw = await _database.StringGetAsync(ClusterMember.CreateKey(KeyPrefix, ServiceName, Host, SelfPort));
            raw.HasValue.Should().BeTrue();
        }

        [Fact(DisplayName = "GetOrCreateAsync should fetch existing entry and refresh LastUpdate")]
        public async Task GetOrCreateShouldFetchAndRefresh()
        {
            await SeedAsync(Host, SelfPort, DateTime.UtcNow - 1.Hours());

            var entity = await _client.GetOrCreateAsync();

            entity.LastUpdate.Should().BeAfter(DateTime.UtcNow - 5.Seconds());
        }

        [Fact(DisplayName = "GetAllAsync should return live members and exclude stale ones")]
        public async Task GetAllShouldFilterStale()
        {
            // self (fresh) + one fresh peer + one stale peer
            await _client.GetOrCreateAsync();
            await SeedAsync("10.0.0.2", 8558, DateTime.UtcNow - 2.Seconds());
            await SeedAsync("10.0.0.3", 8558, DateTime.UtcNow - 10.Minutes());

            var members = await _client.GetAllAsync(30.Seconds());

            members.Count.Should().Be(2);
            members.Should().NotContain(m => m.Host == "10.0.0.3");
        }

        [Fact(DisplayName = "UpdateAsync should refresh LastUpdate")]
        public async Task UpdateShouldRefresh()
        {
            var first = await _client.GetOrCreateAsync();
            await Task.Delay(50.Milliseconds());
            await _client.UpdateAsync();

            var members = await _client.GetAllAsync(TimeSpan.FromMinutes(5));
            members.Should().ContainSingle();
            members[0].LastUpdate.Should().BeAfter(first.LastUpdate);
        }

        [Fact(DisplayName = "RemoveSelfAsync should delete the entry")]
        public async Task RemoveSelfShouldDelete()
        {
            await _client.GetOrCreateAsync();
            await _client.RemoveSelfAsync();

            var raw = await _database.StringGetAsync(ClusterMember.CreateKey(KeyPrefix, ServiceName, Host, SelfPort));
            raw.HasValue.Should().BeFalse();
        }

        [Fact(DisplayName = "Entries should physically expire after their TTL")]
        public async Task EntriesShouldExpire()
        {
            var shortTtl = RedisDiscoverySettings.Empty
                .WithServiceName(ServiceName)
                .WithHostName(Host)
                .WithPort(SelfPort)
                .WithKeyPrefix(KeyPrefix)
                .WithTtlHeartbeatInterval(1.Seconds())
                .WithTtl(2.Seconds());

            var shortClient = new ClusterMemberRedisClient(_connection, shortTtl, Logging.GetLogger(Sys, "short-ttl"));
            await shortClient.GetOrCreateAsync();

            var key = ClusterMember.CreateKey(KeyPrefix, ServiceName, Host, SelfPort);
            (await _database.StringGetAsync(key)).HasValue.Should().BeTrue();

            await Task.Delay(3.Seconds());

            (await _database.StringGetAsync(key)).HasValue.Should().BeFalse();
        }
    }
}
