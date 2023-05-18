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

namespace Akka.Discovery.Config.Hosting;

public class ConfigServiceDiscoveryOptions: IHoconOption
{
    public const string FullPath = "akka.discovery.config";
    
    public string ConfigPath { get; } = "config";
    
    public Type Class { get; } = typeof(ConfigServiceDiscovery);
    
    public List<Service> Services { get; set; } = new (); 

    public void Apply(AkkaConfigurationBuilder builder, Setup? inputSetup = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath} {{");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");
        sb.AppendLine($"services-path = {FullPath}.services");

        if (Services.Count == 0)
            throw new ConfigurationException("There has to be at least one service declared.");

        sb.AppendLine("services {");
        foreach (var service in Services)
        {
            service.Apply(sb);
        }
        sb.AppendLine("}");
        
        sb.AppendLine("}");
        
        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
    }

}