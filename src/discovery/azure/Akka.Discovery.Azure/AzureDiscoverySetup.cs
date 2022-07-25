// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;
using Akka.Actor.Setup;

namespace Akka.Discovery.Azure
{
    public sealed class AzureDiscoverySetup: Setup
    {
        public string ServiceName { get; set; }
        public string HostName { get; set; }
        public int? Port { get; set; }
        public string ConnectionString { get; set; }
        public string TableName { get; set; }
        public TimeSpan? TtlHeartbeatInterval { get; set; }
        public TimeSpan? StaleTtlThreshold { get; set; }
        public TimeSpan? PruneInterval { get; set; }
        public TimeSpan? OperationTimeout { get; set; }
        public TimeSpan? RetryBackoff { get; set; }
        public TimeSpan? MaximumRetryBackoff { get; set; }

        public AzureDiscoverySetup WithServiceName(string serviceName)
        {
            ServiceName = serviceName;
            return this;
        }
        
        public AzureDiscoverySetup WithPublicHostName(string hostName)
        {
            HostName = hostName;
            return this;
        }
        
        public AzureDiscoverySetup WithPublicPort(int port)
        {
            Port = port;
            return this;
        }
        
        public AzureDiscoverySetup WithConnectionString(string connectionString)
        {
            ConnectionString = connectionString;
            return this;
        }
        
        public AzureDiscoverySetup WithTableName(string tableName)
        {
            TableName = tableName;
            return this;
        }

        public AzureDiscoverySetup WithTtlHeartbeatInterval(TimeSpan ttlHeartbeatInterval)
        {
            TtlHeartbeatInterval = ttlHeartbeatInterval;
            return this;
        }
        
        public AzureDiscoverySetup WithStaleTtlThreshold(TimeSpan staleTtlThreshold)
        {
            StaleTtlThreshold = staleTtlThreshold;
            return this;
        }
        
        public AzureDiscoverySetup WithPruneInterval(TimeSpan pruneInterval)
        {
            PruneInterval = pruneInterval;
            return this;
        }

        public AzureDiscoverySetup WithOperationTimeout(TimeSpan operationTimeout)
        {
            OperationTimeout = operationTimeout;
            return this;
        }
        
        public AzureDiscoverySetup WithRetryBackoff(TimeSpan retryBackoff, TimeSpan maximumRetryBackoff)
        {
            RetryBackoff = retryBackoff;
            MaximumRetryBackoff = maximumRetryBackoff;
            return this;
        }
        
        public AzureDiscoverySettings Apply(AzureDiscoverySettings setting)
        {
            if (ServiceName != null)
                setting = setting.WithServiceName(ServiceName);
            if (HostName != null)
                setting = setting.WithPublicHostName(HostName);
            if (Port != null)
                setting = setting.WithPublicPort(Port.Value);
            if (ConnectionString != null)
                setting = setting.WithConnectionString(ConnectionString);
            if (TableName != null)
                setting = setting.WithTableName(TableName);
            if (TtlHeartbeatInterval != null)
                setting = setting.WithTtlHeartbeatInterval(TtlHeartbeatInterval.Value);
            if (StaleTtlThreshold != null)
                setting = setting.WithStaleTtlThreshold(StaleTtlThreshold.Value);
            if (PruneInterval != null)
                setting = setting.WithPruneInterval(PruneInterval.Value);
            if (OperationTimeout != null)
                setting = setting.WithOperationTimeout(OperationTimeout.Value);
            if (RetryBackoff != null && MaximumRetryBackoff != null)
                setting = setting.WithRetryBackoff(RetryBackoff.Value, MaximumRetryBackoff.Value);

            return setting;
        }
    }
}