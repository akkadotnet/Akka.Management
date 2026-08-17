// -----------------------------------------------------------------------
//  <copyright file="RedisDiscoveryOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Text;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Hosting;

namespace Akka.Discovery.Redis
{
    /// <summary>
    /// Akka.Hosting options for Redis-based service discovery
    /// </summary>
    public class RedisDiscoveryOptions : IDiscoveryOptions
    {
        /// <summary>
        /// The configuration path for this discovery method
        /// </summary>
        public string ConfigPath { get; set; } = "redis";

        /// <summary>
        /// The service discovery class type
        /// </summary>
        public Type Class { get; } = typeof(RedisServiceDiscovery);

        /// <summary>
        /// Mark this plugin as the default plugin to be used by ClusterBootstrap
        /// </summary>
        public bool IsDefaultPlugin { get; set; } = true;

        /// <summary>
        /// If set to true, the extension will not register or refresh its own entry in Redis.
        /// Only needs to be set to true if the extension is being used by a read-only consumer
        /// such as ClusterClient contact discovery.
        /// </summary>
        public bool? ReadOnly { get; set; }

        /// <summary>
        /// The public facing IP/host of this node
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// The public open akka management port of this node
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// The service name assigned to the cluster
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// The connection string used to connect to Redis
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// The time-to-live for Redis keys
        /// </summary>
        public TimeSpan? Ttl { get; set; }

        /// <summary>
        /// The time-to-live heartbeat update interval
        /// </summary>
        public TimeSpan? TtlHeartbeatInterval { get; set; }

        /// <summary>
        /// The threshold for a cluster member entry to be considered stale and excluded from lookups.
        /// Override this value by providing a value greater than <see cref="TtlHeartbeatInterval"/>.
        /// If set to 0, this uses min(TtlHeartbeatInterval * 3, Ttl).
        /// </summary>
        public TimeSpan? StaleTtlThreshold { get; set; }

        /// <summary>
        /// The key prefix used for all Redis keys
        /// </summary>
        public string? KeyPrefix { get; set; }

        /// <summary>
        /// The timeout period for all Redis operations. If set, must be greater than zero.
        /// </summary>
        public TimeSpan? OperationTimeout { get; set; }

        /// <summary>
        /// The retry backoff for all Redis operations. If set, must be greater than zero.
        /// </summary>
        public TimeSpan? RetryBackoff { get; set; }

        /// <summary>
        /// The maximum retry backoff for all Redis operations. If set, must be greater than retry-backoff.
        /// </summary>
        public TimeSpan? MaximumRetryBackoff { get; set; }

        /// <summary>
        /// Applies the configuration to the Akka configuration builder
        /// </summary>
        public void Apply(AkkaConfigurationBuilder builder, Setup? inputSetup = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{RedisServiceDiscovery.FullPath(ConfigPath)} {{");
            sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");

            if (ReadOnly is { })
                sb.AppendLine($"read-only = {ReadOnly.ToHocon()}");
            if (HostName is { })
                sb.AppendLine($"public-hostname = {HostName.ToHocon()}");
            if (Port is { })
                sb.AppendLine($"public-port = {Port}");
            if (ServiceName is { })
                sb.AppendLine($"service-name = {ServiceName.ToHocon()}");
            if (ConnectionString is { })
                sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
            if (Ttl is { })
                sb.AppendLine($"ttl = {Ttl.ToHocon()}");
            if (TtlHeartbeatInterval is { })
                sb.AppendLine($"ttl-heartbeat-interval = {TtlHeartbeatInterval.ToHocon()}");
            if (StaleTtlThreshold is { })
                sb.AppendLine($"stale-ttl-threshold = {StaleTtlThreshold.ToHocon()}");
            if (KeyPrefix is { })
                sb.AppendLine($"key-prefix = {KeyPrefix.ToHocon()}");
            if (OperationTimeout is { })
                sb.AppendLine($"operation-timeout = {OperationTimeout.ToHocon()}");
            if (RetryBackoff is { })
                sb.AppendLine($"retry-backoff = {RetryBackoff.ToHocon()}");
            if (MaximumRetryBackoff is { })
                sb.AppendLine($"max-retry-backoff = {MaximumRetryBackoff.ToHocon()}");

            sb.AppendLine("}");

            if (IsDefaultPlugin)
                builder.AddHocon($"akka.discovery.method = {ConfigPath}", HoconAddMode.Prepend);

            builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);

            var fallback = RedisDiscovery.DefaultConfiguration()
                .GetConfig(RedisServiceDiscovery.FullPath(RedisServiceDiscovery.DefaultPath))
                .MoveTo(RedisServiceDiscovery.FullPath(ConfigPath));
            builder.AddHocon(fallback, HoconAddMode.Append);
        }
    }
}
