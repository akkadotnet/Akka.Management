// -----------------------------------------------------------------------
//  <copyright file="HostingSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    public class HostingSpecs
    {
        private static async Task<ActorSystem> StartSystem(Action<AkkaConfigurationBuilder> configure)
        {
            var services = new ServiceCollection();
            services.AddAkka("TestSystem", builder => configure(builder));
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<ActorSystem>();
        }

        [Fact]
        public async Task WithRedisDiscovery_with_connection_string_should_generate_correct_config()
        {
            var system = await StartSystem(builder =>
                builder.WithRedisDiscovery("localhost:6379", "test-service"));

            var config = system.Settings.Config;
            Assert.Equal("redis", config.GetString("akka.discovery.method"));
            Assert.Equal("localhost:6379", config.GetString("akka.discovery.redis.connection-string"));
            Assert.Equal("test-service", config.GetString("akka.discovery.redis.service-name"));

            await system.Terminate();
        }

        [Fact]
        public async Task WithRedisDiscovery_with_options_action_should_generate_correct_config()
        {
            var system = await StartSystem(builder => builder.WithRedisDiscovery(options =>
            {
                options.ConnectionString = "redis-server:6379";
                options.ServiceName = "my-cluster";
                options.Port = 9999;
                options.Ttl = TimeSpan.FromMinutes(5);
                options.ReadOnly = true;
                options.StaleTtlThreshold = TimeSpan.FromSeconds(90);
                options.OperationTimeout = TimeSpan.FromSeconds(7);
            }));

            var config = system.Settings.Config;
            Assert.Equal("redis", config.GetString("akka.discovery.method"));
            Assert.Equal("redis-server:6379", config.GetString("akka.discovery.redis.connection-string"));
            Assert.Equal("my-cluster", config.GetString("akka.discovery.redis.service-name"));
            Assert.Equal(9999, config.GetInt("akka.discovery.redis.public-port"));
            Assert.Equal(TimeSpan.FromMinutes(5), config.GetTimeSpan("akka.discovery.redis.ttl"));
            Assert.True(config.GetBoolean("akka.discovery.redis.read-only"));
            Assert.Equal(TimeSpan.FromSeconds(90), config.GetTimeSpan("akka.discovery.redis.stale-ttl-threshold"));
            Assert.Equal(TimeSpan.FromSeconds(7), config.GetTimeSpan("akka.discovery.redis.operation-timeout"));

            await system.Terminate();
        }

        [Fact]
        public async Task WithRedisDiscovery_should_set_default_discovery_method()
        {
            var system = await StartSystem(builder => builder.WithRedisDiscovery("localhost:6379"));
            Assert.Equal("redis", system.Settings.Config.GetString("akka.discovery.method"));
            await system.Terminate();
        }

        [Fact]
        public async Task WithRedisDiscovery_with_IsDefaultPlugin_false_should_not_set_default_method()
        {
            var system = await StartSystem(builder => builder.WithRedisDiscovery(options =>
            {
                options.ConnectionString = "localhost:6379";
                options.IsDefaultPlugin = false;
            }));

            Assert.False(system.Settings.Config.HasPath("akka.discovery.method"));
            await system.Terminate();
        }
    }
}
