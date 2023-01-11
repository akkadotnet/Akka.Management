// -----------------------------------------------------------------------
//  <copyright file="AkkaDiscoveryOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Hosting;
using Azure.Core;
using Azure.Data.Tables;

namespace Akka.Discovery.Azure;

public class AkkaDiscoveryOptions: IHoconOption
{
    private const string FullPath = "akka.discovery.azure";
    
    public string ConfigPath { get; } = "azure";
    public Type Class { get; } = typeof(AzureServiceDiscovery);
    
    public string? HostName { get; set; }
    public int? Port { get; set; }
    public string? ServiceName { get; set; }
    public string? ConnectionString { get; set; }
    public string? TableName { get; set; }
    public TimeSpan? TtlHeartbeatInterval { get; set; }
    public TimeSpan? StaleTtlThreshold { get; set; }
    public TimeSpan? PruneInterval { get; set; }
    public TimeSpan? OperationTimeout { get; set; }
    public TimeSpan? RetryBackoff { get; set; }
    public TimeSpan? MaximumRetryBackoff { get; set; }
    public Uri? AzureTableEndpoint { get; set; }
    public TokenCredential? AzureCredential { get; set; }
    public TableClientOptions? TableClientOptions { get; set; }

    public void Apply(AkkaConfigurationBuilder builder, Setup? inputSetup = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath} {{");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");

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
        
        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
        builder.AddHocon(AzureServiceDiscovery.DefaultConfig, HoconAddMode.Append);

        if (AzureCredential is { })
        {
            if(AzureTableEndpoint is null)
                throw new ConfigurationException($"Both {nameof(AzureCredential)} and {AzureTableEndpoint} has to be populated to use Azure Identity");

            builder.AddSetup(new AzureDiscoverySetup
            {
                AzureCredential = AzureCredential,
                AzureTableEndpoint = AzureTableEndpoint,
                TableClientOptions = TableClientOptions
            });
        }
    }

}