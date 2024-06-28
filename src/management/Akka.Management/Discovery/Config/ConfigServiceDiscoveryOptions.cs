// -----------------------------------------------------------------------
//  <copyright file="AkkaDiscoveryOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Hosting;

// ReSharper disable once CheckNamespace
namespace Akka.Discovery.Config.Hosting;

public class ConfigServiceDiscoveryOptions: IHoconOption
{
    internal const string DefaultPath = "config";
    internal const string DefaultConfigPath = "akka.discovery." + DefaultPath;
    public static string FullPath(string path) => $"akka.discovery.{path}";
    
    public string ConfigPath { get; set; } = DefaultPath;
    
    public Type Class { get; } = typeof(ConfigServiceDiscovery);
    
    public List<Service> Services { get; set; } = new (); 
    public bool IsDefaultPlugin { get; set; } = true;

    public void Apply(AkkaConfigurationBuilder builder, Setup? inputSetup = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath(ConfigPath)} {{");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");
        sb.AppendLine($"services-path = {FullPath(ConfigPath)}.services");

        if (Services.Count == 0)
            throw new ConfigurationException("There has to be at least one service declared.");

        sb.AppendLine("services {");
        foreach (var service in Services)
        {
            service.Apply(sb);
        }
        sb.AppendLine("}");
        
        sb.AppendLine("}");
        
        if(IsDefaultPlugin)
            sb.AppendLine($"akka.discovery.method = {ConfigPath}");

        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
        
        var fallback = DiscoveryProvider.DefaultConfiguration()
            .GetConfig(DefaultConfigPath)
            .MoveTo(FullPath(ConfigPath));
        builder.AddHocon(fallback, HoconAddMode.Append);
    }

}