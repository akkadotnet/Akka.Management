// -----------------------------------------------------------------------
//  <copyright file="ConfigServiceConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Akka.Discovery.Config;
using Akka.Discovery.Config.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Akka.Management.Tests.Discovery.Config;

public class ConfigServiceConfigSpec
{
    [Fact(DisplayName = "ConfigServiceDiscoveryOptions should generate proper default HOCON config")]
    public void OptionsShouldGenerateDefaultHoconConfig()
    {
        var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "")
            .WithConfigDiscovery(new ConfigServiceDiscoveryOptions
            {
                Services = new List<Service>
                {
                    new Service
                    {
                        Name = "Test",
                        Endpoints = new[] { "abc:1", "def:2" }
                    }
                }
            });
        var systemConfig = builder.Configuration.Value;
        var config = systemConfig.GetConfig(ConfigServiceDiscoveryOptions.DefaultConfigPath);

        Assert.Equal(typeof(ConfigServiceDiscovery), Type.GetType(config.GetString("class")));
        Assert.Equal("akka.discovery.config.services", config.GetString("services-path"));
        // converted from BeEquivalentTo (order-insensitive)
        var defaultEndpoints = config.GetStringList("services.Test.endpoints");
        Assert.Equal(2, defaultEndpoints.Count);
        Assert.Contains("abc:1", defaultEndpoints);
        Assert.Contains("def:2", defaultEndpoints);
    }
    
    [Fact(DisplayName = "ConfigServiceDiscoveryOptions should generate proper HOCON config on the correct config path")]
    public void OptionsShouldGenerateHoconConfig()
    {
        var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "")
            .WithConfigDiscovery(new ConfigServiceDiscoveryOptions
            {
                ConfigPath = "new-config",
                Services = new List<Service>
                {
                    new Service
                    {
                        Name = "Test",
                        Endpoints = new[] { "abc:1", "def:2" }
                    }
                }
            });
        var systemConfig = builder.Configuration.Value;
        var config = systemConfig.GetConfig(ConfigServiceDiscoveryOptions.FullPath("new-config"));

        Assert.Equal(typeof(ConfigServiceDiscovery), Type.GetType(config.GetString("class")));
        Assert.Equal("akka.discovery.new-config.services", config.GetString("services-path"));
        // converted from BeEquivalentTo (order-insensitive)
        var endpoints = config.GetStringList("services.Test.endpoints");
        Assert.Equal(2, endpoints.Count);
        Assert.Contains("abc:1", endpoints);
        Assert.Contains("def:2", endpoints);

        Assert.Null(systemConfig.GetConfig(ConfigServiceDiscoveryOptions.DefaultConfigPath));
    }
    
}