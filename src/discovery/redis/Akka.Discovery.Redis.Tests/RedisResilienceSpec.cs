// -----------------------------------------------------------------------
//  <copyright file="RedisResilienceSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery;
using FluentAssertions;
using FluentAssertions.Extensions;
using Testcontainers.Redis;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    /// <summary>
    /// Proves the plugin's central resilience property: because the Redis connection is established
    /// lazily inside the guardian actor (never in the discovery constructor), an unreachable Redis at
    /// startup neither blocks nor fails ActorSystem startup, and discovery recovers once Redis becomes
    /// reachable.
    /// </summary>
    public class RedisResilienceSpec : TestKit.Xunit.TestKit
    {
        public RedisResilienceSpec(ITestOutputHelper helper)
            : base("akka.loglevel = INFO", nameof(RedisResilienceSpec), helper)
        {
        }

        private static Configuration.Config DiscoveryConfig(string connectionString, string serviceName)
            => ConfigurationFactory.ParseString($@"
                    akka.discovery {{
                        method = redis
                        redis {{
                            connection-string = ""{connectionString}""
                            service-name = ""{serviceName}""
                            public-hostname = ""127.0.0.1""
                            ttl-heartbeat-interval = 1s
                        }}
                    }}")
                .WithFallback(RedisDiscovery.DefaultConfiguration());

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Fact(DisplayName = "Startup should not fail and lookup should return empty when Redis is unreachable")]
        public async Task StartupShouldSurviveUnreachableRedis()
        {
            var config = DiscoveryConfig("127.0.0.1:6390,connectTimeout=500", "svc");
            using var system = ActorSystem.Create("unreachable-redis", config);

            ServiceDiscovery? discovery = null;
            var load = () => discovery = Discovery.Get(system).LoadServiceDiscovery("redis");
            load.Should().NotThrow("the connection must be established lazily, not in the discovery constructor");

            var resolved = await discovery!.Lookup(new Lookup("svc"), 2.Seconds());
            resolved.Addresses.Should().BeEmpty();

            await system.Terminate();
        }

        [Fact(DisplayName = "Discovery should recover once Redis becomes reachable")]
        [Trait("Category", "Integration")]
        public async Task DiscoveryShouldRecoverWhenRedisStarts()
        {
            var hostPort = GetFreePort();

            RedisContainer container;
            try
            {
                container = new RedisBuilder()
                    .WithImage("redis:7.4")
                    .WithPortBinding(hostPort, 6379)
                    .Build();
            }
            catch (Exception e)
            {
                Assert.Skip($"Docker unavailable: {e.Message}");
                return;
            }

            var config = DiscoveryConfig($"127.0.0.1:{hostPort},connectTimeout=500", "recovery");
            using var system = ActorSystem.Create("recovery-redis", config);

            try
            {
                var discovery = Discovery.Get(system).LoadServiceDiscovery("redis");

                // Redis is not up yet: startup survived and lookup is empty.
                var initial = await discovery.Lookup(new Lookup("recovery"), 2.Seconds());
                initial.Addresses.Should().BeEmpty();

                // Bring Redis up; the guardian's retry loop should now register self and resolve it.
                try
                {
                    await container.StartAsync();
                }
                catch (Exception e)
                {
                    // Docker present but the image can't be pulled/started (e.g. Windows CI agents that
                    // can't fetch the linux redis image) — skip rather than fail.
                    Assert.Skip($"Could not start Redis container: {e.Message}");
                    return;
                }

                await AwaitAssertAsync(async () =>
                {
                    var resolved = await discovery.Lookup(new Lookup("recovery"), 3.Seconds());
                    resolved.Addresses.Count.Should().Be(1);
                }, 30.Seconds(), 1.Seconds());
            }
            finally
            {
                await system.Terminate();
                await container.DisposeAsync();
            }
        }
    }
}
