// -----------------------------------------------------------------------
//  <copyright file="EcsServiceDiscoveryOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Akka.Actor.Setup;
using Akka.Hosting;
using Amazon.ECS.Model;

namespace Akka.Discovery.AwsApi.Ecs;

public class EcsServiceDiscoveryOptions: IHoconOption
{
    public string ConfigPath { get; } = AwsEcsDiscovery.DefaultPath;
    internal static string FullPath(string path) => $"akka.discovery.{path}";
    public Type Class { get; } = typeof(EcsServiceDiscovery);
    
    /// <summary>
    ///     Mark this plugin as the default plugin to be used by ClusterBootstrap
    /// </summary>
    public bool IsDefaultPlugin { get; set; } = true;
    
    /// <summary>
    ///     Optional. The name of the AWS ECS cluster.
    /// </summary>
    public string? Cluster { get; set; }
    
    /// <summary>
    ///     Optional. A list of <see cref="Tag"/> used to filter the ECS cluster tasks.
    ///     The task must have the same exact list of tags to be considered as potential contact point by the
    ///     discovery module.
    /// </summary>
    public IEnumerable<Tag>? Tags { get; set; }

    public void Apply(AkkaConfigurationBuilder builder, Setup? setup = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath(ConfigPath)} {{");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");

        if (Cluster is { })
            sb.AppendLine($"cluster = {Cluster.ToHocon()}");

        if (Tags is { })
        {
            var tags = Tags.Select(t => $"{{ key = {t.Key.ToHocon()}, value = {t.Value.ToHocon()} }}");
            sb.AppendLine($"tags = [{string.Join(",", tags)}]");
        }
        
        sb.AppendLine("}");

        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
        
        if(IsDefaultPlugin)
            builder.AddHocon($"akka.discovery.method = {ConfigPath}", HoconAddMode.Prepend);

        var fallback = AwsEcsDiscovery.DefaultConfiguration()
            .GetConfig(AwsEcsDiscovery.DefaultConfigPath)
            .MoveTo(FullPath(ConfigPath));
        builder.AddHocon(fallback, HoconAddMode.Append);
    }
}