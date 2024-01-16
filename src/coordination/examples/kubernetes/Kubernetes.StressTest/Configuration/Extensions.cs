// -----------------------------------------------------------------------
//   <copyright file="Extensions.cs" company="Petabridge, LLC">
//     Copyright (C) 2015-2024 .NET Petabridge, LLC
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kubernetes.StressTest.Configuration;

public static class Extensions
{
    public static AkkaConfigurationBuilder BootstrapFromDocker(
        this AkkaConfigurationBuilder builder,
        IServiceProvider provider,
        Action<RemoteOptions>? remoteConfiguration = null,
        Action<ClusterOptions>? clusterConfiguration = null)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var clusterConfigOptions = configuration.GetSection("cluster").Get<ClusterConfigOptions>();

        var remoteOptions = new RemoteOptions
        {
            HostName = "0.0.0.0",
            PublicHostName = clusterConfigOptions.Ip ?? Dns.GetHostName(),
            Port = clusterConfigOptions.Port
        };
        remoteConfiguration?.Invoke(remoteOptions);
        
        var clusterOptions = new ClusterOptions
        {
            SeedNodes = clusterConfigOptions.Seeds
        };
        clusterConfiguration?.Invoke(clusterOptions);

        var akkaConfig = configuration.GetSection("akka");
        if (akkaConfig.GetChildren().Any())
            builder.AddHocon(akkaConfig, HoconAddMode.Prepend);

        builder.WithRemoting(remoteOptions);
        builder.WithClustering(clusterOptions);
        
        return builder;
    }
}