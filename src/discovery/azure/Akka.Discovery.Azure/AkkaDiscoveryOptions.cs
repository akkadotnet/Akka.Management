// -----------------------------------------------------------------------
//  <copyright file="AkkaDiscoveryOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Hosting;
using Azure.Core;
using Azure.Data.Tables;

namespace Akka.Discovery.Azure;

[Obsolete("Please use AzureDiscoveryOptions instead. Since 1.5.26")]
public class AkkaDiscoveryOptions: IHoconOption
{
    
    public string ConfigPath { get; set; } = "azure";
    public Type Class { get; } = typeof(AzureServiceDiscovery);

    /// <summary>
    ///     Mark this plugin as the default plugin to be used by ClusterBootstrap
    /// </summary>
    public bool IsDefaultPlugin { get; set; } = true;
    
    /// <summary>
    ///     If set to true, the extension will not participate in updating the Azure table.
    ///     Only need to be set to true if the extension is being used by a read only extension 
    ///     such as ClusterClient contact discovery.
    ///     Default value: false
    /// </summary>
    public bool? ReadOnly { get; set; }
    
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
    ///     The service name assigned to the cluster.
    ///     Default value: "default"
    /// </summary>
    public string? ServiceName { get; set; }
    
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

    public void Apply(AkkaConfigurationBuilder builder, Setup? inputSetup = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{AzureServiceDiscovery.FullPath(ConfigPath)} {{");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");

        // We're going to cheat and embed the config path/discovery id here
        // because we need to correlate the setup files with the config
        // during run-time
        sb.AppendLine($"discovery-id = {ConfigPath}");
        
        if (ReadOnly is not null)
            sb.AppendLine($"read-only = {ReadOnly.ToHocon()}");
        if (HostName is { })
            sb.AppendLine($"public-hostname = {HostName.ToHocon()}");
        if (Port is { })
            sb.AppendLine($"public-port = {Port}");
        if (ServiceName is { })
            sb.AppendLine($"service-name = {ServiceName.ToHocon()}");
        if (ConnectionString is { })
            sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
        if (TableName is { })
            sb.AppendLine($"table-name = {TableName.ToHocon()}");
        if (TtlHeartbeatInterval is { })
            sb.AppendLine($"ttl-heartbeat-interval = {TtlHeartbeatInterval.ToHocon()}");
        if (StaleTtlThreshold is { })
            sb.AppendLine($"stale-ttl-threshold = {StaleTtlThreshold.ToHocon()}");
        if (PruneInterval is { })
            sb.AppendLine($"prune-interval = {PruneInterval.ToHocon()}");
        if (OperationTimeout is { })
            sb.AppendLine($"operation-timeout = {OperationTimeout.ToHocon()}");
        if (RetryBackoff is { })
            sb.AppendLine($"retry-backoff = {RetryBackoff.ToHocon()}");
        if (MaximumRetryBackoff is { })
            sb.AppendLine($"max-retry-backoff = {MaximumRetryBackoff.ToHocon()}");
        sb.AppendLine("}");
        
        if(IsDefaultPlugin)
            builder.AddHocon($"akka.discovery.method = {ConfigPath}", HoconAddMode.Prepend);
        
        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);

        var fallback = AzureDiscovery.DefaultConfiguration()
            .GetConfig(AzureServiceDiscovery.FullPath(AzureServiceDiscovery.DefaultPath))
            .MoveTo(AzureServiceDiscovery.FullPath(ConfigPath));
        builder.AddHocon(fallback, HoconAddMode.Append);

        if (AzureCredential is { })
        {
            if(AzureTableEndpoint is null)
                throw new ConfigurationException($"Both {nameof(AzureCredential)} and {AzureTableEndpoint} has to be populated to use Azure Identity");

            var setup = builder.Setups.OfType<AzureDiscoveryMultiSetup>().FirstOrDefault();
            if (setup is null)
            {
                setup = new AzureDiscoveryMultiSetup();
                builder.AddSetup(setup);
            }

            setup.Add(ConfigPath, new AzureDiscoverySetup
            {
                AzureCredential = AzureCredential,
                AzureTableEndpoint = AzureTableEndpoint,
                TableClientOptions = TableClientOptions
            });
        }
        
    }

}