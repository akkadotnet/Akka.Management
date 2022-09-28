// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Akka.Actor.Setup;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;

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
        public Uri AzureTableEndpoint { get; set; }
        public TokenCredential AzureCredential { get; set; }
        public TableClientOptions TableClientOptions { get; set; }
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

        public AzureDiscoverySetup WithAzureCredential(
            Uri azureTableEndpoint,
            TokenCredential credential,
            TableClientOptions tableClientOptions = null)
        {
            AzureTableEndpoint = azureTableEndpoint;
            AzureCredential = credential;
            TableClientOptions = tableClientOptions;
            return this;
        }
        
        public override string ToString()
        {
            var props = new List<string>();
            if(ServiceName != null)
                props.Add($"{nameof(ServiceName)}:{ServiceName}");
            if(HostName != null)
                props.Add($"{nameof(HostName)}:{HostName}");
            if(Port != null)
                props.Add($"{nameof(Port)}:{Port}");
            if(ConnectionString != null)
                props.Add($"{nameof(ConnectionString)}:{ConnectionString}");
            if(TableName != null)
                props.Add($"{nameof(TableName)}:{TableName}");
            if(TtlHeartbeatInterval != null)
                props.Add($"{nameof(TtlHeartbeatInterval)}:{TtlHeartbeatInterval}");
            if(StaleTtlThreshold != null)
                props.Add($"{nameof(StaleTtlThreshold)}:{StaleTtlThreshold}");
            if(PruneInterval != null)
                props.Add($"{nameof(PruneInterval)}:{PruneInterval}");
            if(OperationTimeout != null)
                props.Add($"{nameof(OperationTimeout)}:{OperationTimeout}");
            if(RetryBackoff != null)
                props.Add($"{nameof(RetryBackoff)}:{RetryBackoff}");
            if(MaximumRetryBackoff != null)
                props.Add($"{nameof(MaximumRetryBackoff)}:{MaximumRetryBackoff}");
            if(AzureTableEndpoint != null)
                props.Add($"{nameof(AzureTableEndpoint)}:{AzureTableEndpoint}");
            if(AzureCredential != null)
                props.Add($"{nameof(AzureCredential)}:{AzureCredential}");
            if(TableClientOptions != null)
                props.Add($"{nameof(TableClientOptions)}:{TableClientOptions}");
            
            return $"[AzureDiscoverySetup]({string.Join(", ", props)})";
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
            if (AzureTableEndpoint != null && AzureCredential != null)
                setting = setting.WithAzureCredential(AzureTableEndpoint, AzureCredential, TableClientOptions);

            return setting;
        }
    }
}