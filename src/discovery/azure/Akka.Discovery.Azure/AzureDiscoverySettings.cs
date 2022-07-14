// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;

namespace Akka.Discovery.Azure
{
    public sealed class AzureDiscoverySettings
    {
        public static readonly AzureDiscoverySettings Empty = new AzureDiscoverySettings(
            serviceName: "default",
            connectionString: "<connection-string>",
            tableName: "akka-discovery-cluster-member",
            ttlHeartbeatInterval: TimeSpan.FromMinutes(1),
            staleTtlThreshold: TimeSpan.Zero,
            pruneInterval: TimeSpan.FromHours(1));
        
        public static AzureDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config.GetConfig("akka.discovery.azure"));

        public static AzureDiscoverySettings Create(Configuration.Config config)
            => new AzureDiscoverySettings(
                serviceName: config.GetString("service-name"),
                connectionString: config.GetString("connection-string"),
                tableName: config.GetString("table-name"),
                ttlHeartbeatInterval: config.GetTimeSpan("ttl-heartbeat-interval"),
                staleTtlThreshold: config.GetTimeSpan("stale-ttl-threshold"),
                pruneInterval: config.GetTimeSpan("prune-interval"));
        
        private AzureDiscoverySettings(
            string serviceName,
            string connectionString,
            string tableName,
            TimeSpan ttlHeartbeatInterval,
            TimeSpan staleTtlThreshold,
            TimeSpan pruneInterval)
        {
            if (ttlHeartbeatInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(ttlHeartbeatInterval));
            
            if (pruneInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(pruneInterval));
            
            if (staleTtlThreshold != TimeSpan.Zero && staleTtlThreshold < ttlHeartbeatInterval)
                throw new ArgumentException(
                    $"Must be greater than {nameof(ttlHeartbeatInterval)} if set to non zero",
                    nameof(staleTtlThreshold));

            ServiceName = serviceName;
            ConnectionString = connectionString;
            TableName = tableName;
            TtlHeartbeatInterval = ttlHeartbeatInterval;
            StaleTtlThreshold = staleTtlThreshold;
            PruneInterval = pruneInterval;
        }

        public string ServiceName { get; }
        public string ConnectionString { get; }
        public string TableName { get; }
        public TimeSpan TtlHeartbeatInterval { get; }
        public TimeSpan StaleTtlThreshold { get; }
        public TimeSpan PruneInterval { get; }
        
        public AzureDiscoverySettings WithServiceName(string serviceName)
            => Copy(serviceName: serviceName);
        
        public AzureDiscoverySettings WithConnectionString(string connectionString)
            => Copy(connectionString: connectionString);
        
        public AzureDiscoverySettings WithTableName(string tableName)
            => Copy(tableName: tableName);

        public AzureDiscoverySettings WithTtlHeartbeatInterval(TimeSpan ttlHeartbeatInterval)
            => Copy(ttlHeartbeatInterval: ttlHeartbeatInterval);
        
        public AzureDiscoverySettings WithStaleTtlThreshold(TimeSpan staleTtlThreshold)
            => Copy(staleTtlThreshold: staleTtlThreshold);
        
        public AzureDiscoverySettings WithPruneInterval(TimeSpan pruneInterval)
            => Copy(pruneInterval: pruneInterval);
        
        private AzureDiscoverySettings Copy(
            string serviceName = null,
            string connectionString = null,
            string tableName = null,
            TimeSpan? pruneInterval = null,
            TimeSpan? staleTtlThreshold = null,
            TimeSpan? ttlHeartbeatInterval = null)
            => new AzureDiscoverySettings(
                serviceName: serviceName ?? ServiceName,
                connectionString: connectionString ?? ConnectionString,
                tableName: tableName ?? TableName,
                ttlHeartbeatInterval: ttlHeartbeatInterval ?? TtlHeartbeatInterval,
                staleTtlThreshold: staleTtlThreshold ?? StaleTtlThreshold,
                pruneInterval: pruneInterval ?? PruneInterval);
    }
}