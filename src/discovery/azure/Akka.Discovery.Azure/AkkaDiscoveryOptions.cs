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

public class AkkaDiscoveryOptions: IHoconOption
{
    
    public string ConfigPath { get; set; } = "azure";
    public Type Class { get; } = typeof(AzureServiceDiscovery);

    public bool IsDefaultPlugin { get; set; } = true;
    public bool? ReadOnly { get; set; }
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

        var fallback = AzureServiceDiscovery.DefaultConfig
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