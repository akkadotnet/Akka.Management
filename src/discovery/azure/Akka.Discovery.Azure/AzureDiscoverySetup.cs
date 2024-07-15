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
        /// <summary>
        ///     If set to true, the extension will not participate in updating the Azure table.
        ///     Only need to be set to true if the extension is being used by a read only extension 
        ///     such as ClusterClient contact discovery.
        ///     Default value: false
        /// </summary>
        public bool? ReadOnly { get; set; }
        
        /// <summary>
        ///     The service name assigned to the cluster.
        ///     Default value: "default"
        /// </summary>
        public string? ServiceName { get; set; }
        
        /// <summary>
        ///     The public facing IP/host of this node
        ///     akka.remote.dot-netty.tcp.public-hostname is used if not overriden or empty.
        ///     if akka.remote.dot-netty.tcp.public-hostname is empty, Dns.GetHostName is used.
        /// </summary>
        public string? HostName { get; set; }
        
        /// <summary>
        ///     The public open akka management port of this node 
        ///     The value will need to be from 1 to 65535, auto-assign port (0) is not supported.
        ///     Default value: 8558
        /// </summary>
        public int? Port { get; set; }
        
        /// <summary>
        ///     The connection string used to connect to Azure Table hosting the cluster membership table.
        /// </summary>
        public string? ConnectionString { get; set; }
        
        /// <summary>
        ///     The azure table name used to store cluster membership entries. 
        ///     Default value: "akkaclustermembers"
        /// </summary>
        public string? TableName { get; set; }
        
        /// <summary>
        ///     The time-to-live heartbeat update interval.
        ///     Default value: 1 minute
        /// </summary>
        public TimeSpan? TtlHeartbeatInterval { get; set; }
    
        /// <summary>
        ///     The threshold for a cluster member entry to be considered stale.
        ///     Override this value by providing a value greater than TtlHeartbeatInterval.
        ///         * If set to 0, this will use the value (TtlHeartbeatInterval * 5).
        ///         * If set to a value less than TtlHeartbeatInterval, discovery WILL throw an exception.
        /// </summary>
        public TimeSpan? StaleTtlThreshold { get; set; }
    
        /// <summary>
        ///     The stale data pruning interval.
        ///     Default value: 1 hour
        /// </summary>
        public TimeSpan? PruneInterval { get; set; }
    
        /// <summary>
        ///     The timeout period for all Azure Tables API HTTP operation. If set, must be greater than zero.
        ///     Default value: 10 seconds
        /// </summary>
        public TimeSpan? OperationTimeout { get; set; }
    
        /// <summary>
        ///     The retry backoff for all HTTP operation. If set, must be greater than zero.
        ///     Default value: 500 milliseconds
        /// </summary>
        public TimeSpan? RetryBackoff { get; set; }
    
        /// <summary>
        ///     The maximum retry backoff for all HTTP operations. If set, must be greater than retry-backoff.
        ///     Default value: 5 seconds
        /// </summary>
        public TimeSpan? MaximumRetryBackoff { get; set; }
        
        /// <summary>
        ///     A <see cref="Uri"/> referencing the table service account.
        ///     This is likely to be similar to "https:// {account_name}.table.core.windows.net"
        ///     or "https:// {account_name}.table.cosmos.azure.com".
        ///     If you set the <see cref="AzureCredential"/> property, This property MUST NOT be null.
        /// </summary>
        public Uri? AzureTableEndpoint { get; set; }
    
        /// <summary>
        ///     The <see cref="TokenCredential"/> used to authorize requests.
        /// </summary>
        public TokenCredential? AzureCredential { get; set; }
    
        /// <summary>
        ///     Optional client options that define the transport pipeline policies for authentication,
        ///     retries, etc., that are applied to every request.
        /// </summary>
        public TableClientOptions? TableClientOptions { get; set; }
        
        public AzureDiscoverySetup WithReadOnlyMode(bool readOnly)
        {
            ReadOnly = readOnly;
            return this;
        }
        
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
            TableClientOptions? tableClientOptions = null)
        {
            AzureTableEndpoint = azureTableEndpoint;
            AzureCredential = credential;
            TableClientOptions = tableClientOptions;
            return this;
        }
        
        public override string ToString()
        {
            var props = new List<string>();
            if(ReadOnly != null)
                props.Add($"{nameof(ReadOnly)}:{ReadOnly}");
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
            if (ReadOnly != null)
                setting = setting.WithReadOnlyMode(ReadOnly.Value);
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