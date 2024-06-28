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
using FluentAssertions;
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

        Type.GetType(config.GetString("class")).Should().Be(typeof(ConfigServiceDiscovery));
        config.GetString("services-path").Should().Be("akka.discovery.config.services");
        config.GetStringList("services.Test.endpoints").Should().BeEquivalentTo("abc:1", "def:2");
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

        Type.GetType(config.GetString("class")).Should().Be(typeof(ConfigServiceDiscovery));
        config.GetString("services-path").Should().Be("akka.discovery.new-config.services");
        config.GetStringList("services.Test.endpoints").Should().BeEquivalentTo("abc:1", "def:2");

        systemConfig.GetConfig(ConfigServiceDiscoveryOptions.DefaultConfigPath).Should().BeNull();
    }
    
}