// -----------------------------------------------------------------------
//  <copyright file="RedisDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Net;
using Akka.Actor;
using Akka.Configuration;

namespace Akka.Discovery.Redis
{
    /// <summary>
    /// Settings for the Redis-based service discovery plugin
    /// </summary>
    public sealed class RedisDiscoverySettings
    {
        /// <summary>
        /// Default empty settings
        /// </summary>
        public static readonly RedisDiscoverySettings Empty = new RedisDiscoverySettings(
            readOnly: false,
            serviceName: "default",
            hostName: Dns.GetHostName(),
            port: 8558,
            connectionString: "<connection-string>",
            ttl: TimeSpan.FromMinutes(2),
            ttlHeartbeatInterval: TimeSpan.FromSeconds(30),
            staleTtlThreshold: TimeSpan.Zero,
            keyPrefix: "akka:discovery",
            operationTimeout: TimeSpan.FromSeconds(10),
            retryBackoff: TimeSpan.FromMilliseconds(500),
            maximumRetryBackoff: TimeSpan.FromSeconds(5));

        /// <summary>
        /// Creates settings from the actor system configuration
        /// </summary>
        /// <param name="system">The actor system</param>
        /// <returns>Settings instance</returns>
        public static RedisDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config);

        /// <summary>
        /// Creates settings from the full actor system configuration
        /// </summary>
        /// <param name="systemConfig">The full actor system configuration</param>
        /// <returns>Settings instance</returns>
        public static RedisDiscoverySettings Create(Configuration.Config systemConfig)
            => Create(systemConfig, systemConfig.GetConfig("akka.discovery.redis"));

        /// <summary>
        /// Creates settings from the actor system and a specific discovery configuration section
        /// </summary>
        /// <param name="system">The actor system</param>
        /// <param name="config">The discovery configuration section to read from</param>
        /// <returns>Settings instance</returns>
        public static RedisDiscoverySettings Create(ActorSystem system, Configuration.Config config)
            => Create(system.Settings.Config, config);

        private static RedisDiscoverySettings Create(Configuration.Config systemConfig, Configuration.Config config)
        {
            // The public hostname falls back to the remoting public-hostname, and finally to the
            // machine hostname. This keeps the registered contact point correct in containerized
            // and remoted deployments where the discovery section leaves public-hostname empty.
            var host = config.GetString("public-hostname");
            if (string.IsNullOrWhiteSpace(host))
            {
                host = systemConfig.GetString("akka.remote.dot-netty.tcp.public-hostname");
                if (string.IsNullOrWhiteSpace(host))
                    host = Dns.GetHostName();
            }

            var connectionString = config.GetString("connection-string");
            if (connectionString == "<connection-string>")
                throw new ConfigurationException(
                    "akka.discovery.redis.connection-string must be set to a real Redis connection string");

            return new RedisDiscoverySettings(
                readOnly: config.GetBoolean("read-only"),
                serviceName: config.GetString("service-name"),
                hostName: host,
                port: config.GetInt("public-port"),
                connectionString: connectionString,
                ttl: config.GetTimeSpan("ttl"),
                ttlHeartbeatInterval: config.GetTimeSpan("ttl-heartbeat-interval"),
                staleTtlThreshold: config.GetTimeSpan("stale-ttl-threshold"),
                keyPrefix: config.GetString("key-prefix"),
                operationTimeout: config.GetTimeSpan("operation-timeout"),
                retryBackoff: config.GetTimeSpan("retry-backoff"),
                maximumRetryBackoff: config.GetTimeSpan("max-retry-backoff"));
        }

        private RedisDiscoverySettings(
            bool readOnly,
            string serviceName,
            string hostName,
            int port,
            string connectionString,
            TimeSpan ttl,
            TimeSpan ttlHeartbeatInterval,
            TimeSpan staleTtlThreshold,
            string keyPrefix,
            TimeSpan operationTimeout,
            TimeSpan retryBackoff,
            TimeSpan maximumRetryBackoff)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Must not be empty or whitespace", nameof(serviceName));

            if (string.IsNullOrWhiteSpace(hostName))
                throw new ArgumentException("Must not be empty or whitespace", nameof(hostName));

            if (port < 1 || port > 65535)
                throw new ArgumentException("Must be greater than zero and less than or equal to 65535", nameof(port));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Must not be empty or whitespace", nameof(connectionString));

            if (ttl <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(ttl));

            if (ttlHeartbeatInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(ttlHeartbeatInterval));

            if (ttlHeartbeatInterval >= ttl)
                throw new ArgumentException("Must be less than ttl", nameof(ttlHeartbeatInterval));

            if (staleTtlThreshold != TimeSpan.Zero && staleTtlThreshold <= ttlHeartbeatInterval)
                throw new ArgumentException(
                    $"Must be greater than {nameof(ttlHeartbeatInterval)} if set to non zero",
                    nameof(staleTtlThreshold));

            if (string.IsNullOrWhiteSpace(keyPrefix))
                throw new ArgumentException("Must not be empty or whitespace", nameof(keyPrefix));

            if (operationTimeout <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(operationTimeout));

            if (retryBackoff <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(retryBackoff));

            if (maximumRetryBackoff < retryBackoff)
                throw new ArgumentException($"Must be greater than {nameof(retryBackoff)}", nameof(maximumRetryBackoff));

            ReadOnly = readOnly;
            ServiceName = serviceName;
            HostName = hostName;
            Port = port;
            ConnectionString = connectionString;
            Ttl = ttl;
            TtlHeartbeatInterval = ttlHeartbeatInterval;
            StaleTtlThreshold = staleTtlThreshold;
            KeyPrefix = keyPrefix;
            OperationTimeout = operationTimeout;
            RetryBackoff = retryBackoff;
            MaximumRetryBackoff = maximumRetryBackoff;
        }

        /// <summary>
        /// If set to true, the extension will not register or refresh its own entry in Redis.
        /// </summary>
        public bool ReadOnly { get; }

        /// <summary>
        /// The service name assigned to the cluster
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// The public facing hostname of this node
        /// </summary>
        public string HostName { get; }

        /// <summary>
        /// The public open akka management port of this node
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// The connection string used to connect to Redis
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// The time-to-live for Redis keys
        /// </summary>
        public TimeSpan Ttl { get; }

        /// <summary>
        /// The time-to-live heartbeat update interval
        /// </summary>
        public TimeSpan TtlHeartbeatInterval { get; }

        /// <summary>
        /// The threshold for a cluster member entry to be considered stale and excluded from lookups
        /// </summary>
        public TimeSpan StaleTtlThreshold { get; }

        /// <summary>
        /// The key prefix used for all Redis keys
        /// </summary>
        public string KeyPrefix { get; }

        /// <summary>
        /// The timeout period for all Redis operations
        /// </summary>
        public TimeSpan OperationTimeout { get; }

        /// <summary>
        /// The retry backoff for all Redis operations
        /// </summary>
        public TimeSpan RetryBackoff { get; }

        /// <summary>
        /// The maximum retry backoff for all Redis operations
        /// </summary>
        public TimeSpan MaximumRetryBackoff { get; }

        /// <summary>
        /// The effective stale TTL threshold used at lookup time. When <see cref="StaleTtlThreshold"/>
        /// is zero this resolves to min(TtlHeartbeatInterval * 3, Ttl) so that dead nodes drop out of
        /// lookups before their Redis key physically expires, while never exceeding the key TTL.
        /// </summary>
        public TimeSpan EffectiveStaleTtlThreshold
        {
            get
            {
                if (StaleTtlThreshold != TimeSpan.Zero)
                    return StaleTtlThreshold;

                var defaultThreshold = new TimeSpan(TtlHeartbeatInterval.Ticks * 3);
                return defaultThreshold < Ttl ? defaultThreshold : Ttl;
            }
        }

        /// <summary>
        /// Creates a copy with a different read-only mode
        /// </summary>
        public RedisDiscoverySettings WithReadOnlyMode(bool readOnly)
            => Copy(readOnly: readOnly);

        /// <summary>
        /// Creates a copy with a different service name
        /// </summary>
        public RedisDiscoverySettings WithServiceName(string serviceName)
            => Copy(serviceName: serviceName);

        /// <summary>
        /// Creates a copy with a different hostname
        /// </summary>
        public RedisDiscoverySettings WithHostName(string hostName)
            => Copy(hostName: hostName);

        /// <summary>
        /// Creates a copy with a different port
        /// </summary>
        public RedisDiscoverySettings WithPort(int port)
            => Copy(port: port);

        /// <summary>
        /// Creates a copy with a different connection string
        /// </summary>
        public RedisDiscoverySettings WithConnectionString(string connectionString)
            => Copy(connectionString: connectionString);

        /// <summary>
        /// Creates a copy with a different TTL
        /// </summary>
        public RedisDiscoverySettings WithTtl(TimeSpan ttl)
            => Copy(ttl: ttl);

        /// <summary>
        /// Creates a copy with a different TTL heartbeat interval
        /// </summary>
        public RedisDiscoverySettings WithTtlHeartbeatInterval(TimeSpan ttlHeartbeatInterval)
            => Copy(ttlHeartbeatInterval: ttlHeartbeatInterval);

        /// <summary>
        /// Creates a copy with a different stale TTL threshold
        /// </summary>
        public RedisDiscoverySettings WithStaleTtlThreshold(TimeSpan staleTtlThreshold)
            => Copy(staleTtlThreshold: staleTtlThreshold);

        /// <summary>
        /// Creates a copy with a different key prefix
        /// </summary>
        public RedisDiscoverySettings WithKeyPrefix(string keyPrefix)
            => Copy(keyPrefix: keyPrefix);

        /// <summary>
        /// Creates a copy with a different operation timeout
        /// </summary>
        public RedisDiscoverySettings WithOperationTimeout(TimeSpan operationTimeout)
            => Copy(operationTimeout: operationTimeout);

        /// <summary>
        /// Creates a copy with different retry backoff settings
        /// </summary>
        public RedisDiscoverySettings WithRetryBackoff(TimeSpan retryBackoff, TimeSpan maximumRetryBackoff)
            => Copy(retryBackoff: retryBackoff, maximumRetryBackoff: maximumRetryBackoff);

        private RedisDiscoverySettings Copy(
            bool? readOnly = null,
            string? serviceName = null,
            string? hostName = null,
            int? port = null,
            string? connectionString = null,
            TimeSpan? ttl = null,
            TimeSpan? ttlHeartbeatInterval = null,
            TimeSpan? staleTtlThreshold = null,
            string? keyPrefix = null,
            TimeSpan? operationTimeout = null,
            TimeSpan? retryBackoff = null,
            TimeSpan? maximumRetryBackoff = null)
            => new(
                readOnly: readOnly ?? ReadOnly,
                serviceName: serviceName ?? ServiceName,
                hostName: hostName ?? HostName,
                port: port ?? Port,
                connectionString: connectionString ?? ConnectionString,
                ttl: ttl ?? Ttl,
                ttlHeartbeatInterval: ttlHeartbeatInterval ?? TtlHeartbeatInterval,
                staleTtlThreshold: staleTtlThreshold ?? StaleTtlThreshold,
                keyPrefix: keyPrefix ?? KeyPrefix,
                operationTimeout: operationTimeout ?? OperationTimeout,
                retryBackoff: retryBackoff ?? RetryBackoff,
                maximumRetryBackoff: maximumRetryBackoff ?? MaximumRetryBackoff);

        /// <inheritdoc/>
        public override string ToString()
            => $"[RedisDiscoverySettings](" +
               $"{nameof(ReadOnly)}:{ReadOnly}, " +
               $"{nameof(ServiceName)}:{ServiceName}, " +
               $"{nameof(HostName)}:{HostName}, " +
               $"{nameof(Port)}:{Port}, " +
               $"{nameof(ConnectionString)}:{ConnectionString}, " +
               $"{nameof(Ttl)}:{Ttl}, " +
               $"{nameof(TtlHeartbeatInterval)}:{TtlHeartbeatInterval}, " +
               $"{nameof(StaleTtlThreshold)}:{StaleTtlThreshold}, " +
               $"{nameof(KeyPrefix)}:{KeyPrefix}, " +
               $"{nameof(OperationTimeout)}:{OperationTimeout}, " +
               $"{nameof(RetryBackoff)}:{RetryBackoff}, " +
               $"{nameof(MaximumRetryBackoff)}:{MaximumRetryBackoff})";
    }
}
