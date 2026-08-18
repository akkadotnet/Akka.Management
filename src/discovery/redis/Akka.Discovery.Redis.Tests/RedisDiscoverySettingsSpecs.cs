// -----------------------------------------------------------------------
//  <copyright file="RedisDiscoverySettingsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using Akka.Configuration;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    public class RedisDiscoverySettingsSpecs
    {
        private static RedisDiscoverySettings ParseSettings(string redisHocon, string extraSystemHocon = "")
        {
            var hocon = $"akka.discovery.redis {{\n{redisHocon}\n}}\n{extraSystemHocon}";
            var config = ConfigurationFactory.ParseString(hocon)
                .WithFallback(RedisDiscovery.DefaultConfiguration());
            return RedisDiscoverySettings.Create(config);
        }

        [Fact]
        public void Create_should_parse_HOCON_configuration_correctly()
        {
            var settings = ParseSettings(@"
                read-only = true
                service-name = ""my-service""
                public-hostname = ""localhost""
                public-port = 9999
                connection-string = ""redis:6379""
                ttl = 5m
                ttl-heartbeat-interval = 45s
                stale-ttl-threshold = 90s
                key-prefix = ""test:prefix""
                operation-timeout = 7s
                retry-backoff = 250ms
                max-retry-backoff = 3s
            ");

            Assert.True(settings.ReadOnly);
            Assert.Equal("my-service", settings.ServiceName);
            Assert.Equal("localhost", settings.HostName);
            Assert.Equal(9999, settings.Port);
            Assert.Equal("redis:6379", settings.ConnectionString);
            Assert.Equal(TimeSpan.FromMinutes(5), settings.Ttl);
            Assert.Equal(TimeSpan.FromSeconds(45), settings.TtlHeartbeatInterval);
            Assert.Equal(TimeSpan.FromSeconds(90), settings.StaleTtlThreshold);
            Assert.Equal("test:prefix", settings.KeyPrefix);
            Assert.Equal(TimeSpan.FromSeconds(7), settings.OperationTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(250), settings.RetryBackoff);
            Assert.Equal(TimeSpan.FromSeconds(3), settings.MaximumRetryBackoff);
        }

        [Fact]
        public void Create_should_use_reference_conf_defaults()
        {
            var settings = ParseSettings(@"connection-string = ""localhost""");

            Assert.False(settings.ReadOnly);
            Assert.Equal("default", settings.ServiceName);
            Assert.Equal(8558, settings.Port);
            Assert.Equal(TimeSpan.FromMinutes(2), settings.Ttl);
            Assert.Equal(TimeSpan.FromSeconds(30), settings.TtlHeartbeatInterval);
            Assert.Equal(TimeSpan.Zero, settings.StaleTtlThreshold);
            Assert.Equal("akka:discovery", settings.KeyPrefix);
            Assert.Equal(TimeSpan.FromSeconds(10), settings.OperationTimeout);
            Assert.Equal(TimeSpan.FromMilliseconds(500), settings.RetryBackoff);
            Assert.Equal(TimeSpan.FromSeconds(5), settings.MaximumRetryBackoff);
        }

        [Fact]
        public void Create_should_fall_back_to_remoting_public_hostname_when_discovery_hostname_empty()
        {
            var settings = ParseSettings(
                redisHocon: @"connection-string = ""localhost""
                              public-hostname = """"",
                extraSystemHocon: @"akka.remote.dot-netty.tcp.public-hostname = ""my-remote-host""");

            Assert.Equal("my-remote-host", settings.HostName);
        }

        [Fact]
        public void EffectiveStaleTtlThreshold_should_default_to_three_heartbeats_bounded_by_ttl()
        {
            // heartbeat 30s, ttl 2m => min(90s, 120s) == 90s
            var settings = ParseSettings(@"connection-string = ""localhost""");
            Assert.Equal(TimeSpan.FromSeconds(90), settings.EffectiveStaleTtlThreshold);
        }

        [Fact]
        public void EffectiveStaleTtlThreshold_should_use_explicit_value_when_set()
        {
            var settings = ParseSettings(@"
                connection-string = ""localhost""
                stale-ttl-threshold = 50s
            ");
            Assert.Equal(TimeSpan.FromSeconds(50), settings.EffectiveStaleTtlThreshold);
        }

        [Fact]
        public void WithServiceName_should_create_modified_copy()
        {
            var original = RedisDiscoverySettings.Empty;
            var modified = original.WithServiceName("new-service");

            Assert.Equal("new-service", modified.ServiceName);
            Assert.Equal(original.HostName, modified.HostName);
            Assert.Equal(original.Port, modified.Port);
            Assert.Equal("default", original.ServiceName);
        }

        [Fact]
        public void WithReadOnlyMode_should_create_modified_copy()
        {
            var original = RedisDiscoverySettings.Empty;
            var modified = original.WithReadOnlyMode(true);

            Assert.True(modified.ReadOnly);
            Assert.False(original.ReadOnly);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(70000)]
        public void Constructor_should_throw_when_port_is_invalid(int port)
        {
            var act = () => RedisDiscoverySettings.Empty.WithPort(port);
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_should_throw_when_ttl_heartbeat_interval_exceeds_ttl()
        {
            var act = () => RedisDiscoverySettings.Empty
                .WithTtl(TimeSpan.FromSeconds(30))
                .WithTtlHeartbeatInterval(TimeSpan.FromSeconds(45));

            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_should_throw_when_stale_ttl_threshold_not_greater_than_heartbeat()
        {
            // heartbeat is 30s by default; 10s <= 30s must throw
            var act = () => RedisDiscoverySettings.Empty.WithStaleTtlThreshold(TimeSpan.FromSeconds(10));
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_should_throw_when_operation_timeout_is_not_positive()
        {
            var act = () => RedisDiscoverySettings.Empty.WithOperationTimeout(TimeSpan.Zero);
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_should_throw_when_max_retry_backoff_less_than_retry_backoff()
        {
            var act = () => RedisDiscoverySettings.Empty
                .WithRetryBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_should_throw_on_empty_connection_string()
        {
            var act = () => RedisDiscoverySettings.Empty.WithConnectionString("  ");
            Assert.Throws<ArgumentException>(act);
        }

        [Fact]
        public void Constructor_should_throw_on_empty_key_prefix()
        {
            var act = () => RedisDiscoverySettings.Empty.WithKeyPrefix("");
            Assert.Throws<ArgumentException>(act);
        }
    }
}
