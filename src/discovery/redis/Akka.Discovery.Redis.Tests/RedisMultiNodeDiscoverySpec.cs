// -----------------------------------------------------------------------
//  <copyright file="RedisMultiNodeDiscoverySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    /// <summary>
    /// Plugin-level multi-node convergence: several independent nodes register themselves in Redis
    /// under the same service name and each must discover all peers. This proves the discovery
    /// mechanism converges without the Aspire/ClusterBootstrap layer. End-to-end cluster formation
    /// (bootstrap -> members Up) is covered separately via Aspire's native testing in the Aspire
    /// hosting test project.
    /// </summary>
    [Collection(nameof(RedisSpecs))]
    [Trait("Category", "Integration")]
    public class RedisMultiNodeDiscoverySpec : TestKit.Xunit.TestKit, IAsyncLifetime
    {
        private const string ServiceName = "multinode";
        private const int NodeCount = 3;
        private const int BasePort = 9550;

        private readonly RedisFixture _fixture;
        private readonly List<ActorSystem> _systems = new();
        private readonly List<ServiceDiscovery> _discoveries = new();

        public RedisMultiNodeDiscoverySpec(ITestOutputHelper helper, RedisFixture fixture)
            : base("akka.loglevel = INFO", nameof(RedisMultiNodeDiscoverySpec), helper)
        {
            _fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            _fixture.EnsureAvailable();
            await _fixture.ClearAsync();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var system in _systems)
                await system.Terminate();
        }

        private Configuration.Config NodeConfig(int index)
            => ConfigurationFactory.ParseString($@"
                    akka.loglevel = INFO
                    akka.discovery {{
                        method = redis
                        redis {{
                            connection-string = ""{_fixture.ConnectionString}""
                            service-name = ""{ServiceName}""
                            public-hostname = ""127.0.0.1""
                            public-port = {BasePort + index}
                            ttl-heartbeat-interval = 1s
                        }}
                    }}")
                .WithFallback(RedisDiscovery.DefaultConfiguration());

        [Fact(DisplayName = "All nodes should discover every peer registered in Redis")]
        public async Task AllNodesShouldDiscoverEachOther()
        {
            for (var i = 0; i < NodeCount; i++)
            {
                var system = ActorSystem.Create($"node-{i}", NodeConfig(i));
                _systems.Add(system);
                // Loading the discovery instance starts the guardian, which registers this node.
                _discoveries.Add(Discovery.Get(system).LoadServiceDiscovery("redis"));
            }

            // Every node must resolve all NodeCount contact points.
            await AwaitAssertAsync(async () =>
            {
                foreach (var discovery in _discoveries)
                {
                    var resolved = await discovery.Lookup(new Lookup(ServiceName), TimeSpan.FromSeconds(5));
                    Assert.Equal(NodeCount, resolved.Addresses.Count);
                }
            }, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
        }
    }
}
