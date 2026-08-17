// -----------------------------------------------------------------------
//  <copyright file="RedisDiscoverySettingsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using Akka.Configuration;
using FluentAssertions;
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

            settings.ReadOnly.Should().BeTrue();
            settings.ServiceName.Should().Be("my-service");
            settings.HostName.Should().Be("localhost");
            settings.Port.Should().Be(9999);
            settings.ConnectionString.Should().Be("redis:6379");
            settings.Ttl.Should().Be(TimeSpan.FromMinutes(5));
            settings.TtlHeartbeatInterval.Should().Be(TimeSpan.FromSeconds(45));
            settings.StaleTtlThreshold.Should().Be(TimeSpan.FromSeconds(90));
            settings.KeyPrefix.Should().Be("test:prefix");
            settings.OperationTimeout.Should().Be(TimeSpan.FromSeconds(7));
            settings.RetryBackoff.Should().Be(TimeSpan.FromMilliseconds(250));
            settings.MaximumRetryBackoff.Should().Be(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public void Create_should_use_reference_conf_defaults()
        {
            var settings = ParseSettings(@"connection-string = ""localhost""");

            settings.ReadOnly.Should().BeFalse();
            settings.ServiceName.Should().Be("default");
            settings.Port.Should().Be(8558);
            settings.Ttl.Should().Be(TimeSpan.FromMinutes(2));
            settings.TtlHeartbeatInterval.Should().Be(TimeSpan.FromSeconds(30));
            settings.StaleTtlThreshold.Should().Be(TimeSpan.Zero);
            settings.KeyPrefix.Should().Be("akka:discovery");
            settings.OperationTimeout.Should().Be(TimeSpan.FromSeconds(10));
            settings.RetryBackoff.Should().Be(TimeSpan.FromMilliseconds(500));
            settings.MaximumRetryBackoff.Should().Be(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Create_should_fall_back_to_remoting_public_hostname_when_discovery_hostname_empty()
        {
            var settings = ParseSettings(
                redisHocon: @"connection-string = ""localhost""
                              public-hostname = """"",
                extraSystemHocon: @"akka.remote.dot-netty.tcp.public-hostname = ""my-remote-host""");

            settings.HostName.Should().Be("my-remote-host");
        }

        [Fact]
        public void EffectiveStaleTtlThreshold_should_default_to_three_heartbeats_bounded_by_ttl()
        {
            // heartbeat 30s, ttl 2m => min(90s, 120s) == 90s
            var settings = ParseSettings(@"connection-string = ""localhost""");
            settings.EffectiveStaleTtlThreshold.Should().Be(TimeSpan.FromSeconds(90));
        }

        [Fact]
        public void EffectiveStaleTtlThreshold_should_use_explicit_value_when_set()
        {
            var settings = ParseSettings(@"
                connection-string = ""localhost""
                stale-ttl-threshold = 50s
            ");
            settings.EffectiveStaleTtlThreshold.Should().Be(TimeSpan.FromSeconds(50));
        }

        [Fact]
        public void WithServiceName_should_create_modified_copy()
        {
            var original = RedisDiscoverySettings.Empty;
            var modified = original.WithServiceName("new-service");

            modified.ServiceName.Should().Be("new-service");
            modified.HostName.Should().Be(original.HostName);
            modified.Port.Should().Be(original.Port);
            original.ServiceName.Should().Be("default");
        }

        [Fact]
        public void WithReadOnlyMode_should_create_modified_copy()
        {
            var original = RedisDiscoverySettings.Empty;
            var modified = original.WithReadOnlyMode(true);

            modified.ReadOnly.Should().BeTrue();
            original.ReadOnly.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(70000)]
        public void Constructor_should_throw_when_port_is_invalid(int port)
        {
            var act = () => RedisDiscoverySettings.Empty.WithPort(port);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_should_throw_when_ttl_heartbeat_interval_exceeds_ttl()
        {
            var act = () => RedisDiscoverySettings.Empty
                .WithTtl(TimeSpan.FromSeconds(30))
                .WithTtlHeartbeatInterval(TimeSpan.FromSeconds(45));

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_should_throw_when_stale_ttl_threshold_not_greater_than_heartbeat()
        {
            // heartbeat is 30s by default; 10s <= 30s must throw
            var act = () => RedisDiscoverySettings.Empty.WithStaleTtlThreshold(TimeSpan.FromSeconds(10));
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_should_throw_when_operation_timeout_is_not_positive()
        {
            var act = () => RedisDiscoverySettings.Empty.WithOperationTimeout(TimeSpan.Zero);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_should_throw_when_max_retry_backoff_less_than_retry_backoff()
        {
            var act = () => RedisDiscoverySettings.Empty
                .WithRetryBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_should_throw_on_empty_connection_string()
        {
            var act = () => RedisDiscoverySettings.Empty.WithConnectionString("  ");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_should_throw_on_empty_key_prefix()
        {
            var act = () => RedisDiscoverySettings.Empty.WithKeyPrefix("");
            act.Should().Throw<ArgumentException>();
        }
    }
}
