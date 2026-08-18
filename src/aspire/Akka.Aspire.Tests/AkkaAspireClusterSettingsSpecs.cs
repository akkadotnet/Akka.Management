// -----------------------------------------------------------------------
//  <copyright file="AkkaAspireClusterSettingsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Xunit;

namespace Akka.Aspire.Tests;

public class AkkaAspireClusterSettingsSpecs
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var settings = new AkkaAspireClusterSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(8081, settings.RemotePort);
        Assert.Equal(8558, settings.ManagementPort);
        Assert.Equal("localhost", settings.PublicHostName);
        Assert.Equal("default", settings.ServiceName);
        Assert.Equal(1, settings.RequiredContactPointsNr);
        Assert.False(settings.FilterOnFallbackPort);
        Assert.Null(settings.Clustering);
    }

    [Fact]
    public void BindFromConfiguration_WithCustomValues_ShouldPopulateSettings()
    {
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:RemotePort", "9999" },
            { "Akka:Cluster:ManagementPort", "7777" },
            { "Akka:Cluster:PublicHostName", "myhost.example.com" },
            { "Akka:Cluster:ServiceName", "my-service" },
            { "Akka:Cluster:RequiredContactPointsNr", "3" },
            { "Akka:Cluster:FilterOnFallbackPort", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var settings = new AkkaAspireClusterSettings();
        configuration.GetSection("Akka:Cluster").Bind(settings);

        Assert.True(settings.Enabled);
        Assert.Equal(9999, settings.RemotePort);
        Assert.Equal(7777, settings.ManagementPort);
        Assert.Equal("myhost.example.com", settings.PublicHostName);
        Assert.Equal("my-service", settings.ServiceName);
        Assert.Equal(3, settings.RequiredContactPointsNr);
        Assert.True(settings.FilterOnFallbackPort);
    }

    [Fact]
    public void BindFromConfiguration_WithClusteringSection_ShouldBindProperly()
    {
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:Clustering:ProviderType", "Redis" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var settings = new AkkaAspireClusterSettings();
        configuration.GetSection("Akka:Cluster").Bind(settings);

        Assert.True(settings.Enabled);
        Assert.NotNull(settings.Clustering);
        Assert.Equal("Redis", settings.Clustering!.ProviderType);
    }

    [Fact]
    public void BindFromConfiguration_WithPartialConfiguration_ShouldUseDefaults()
    {
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:ServiceName", "test-service" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var settings = new AkkaAspireClusterSettings();
        configuration.GetSection("Akka:Cluster").Bind(settings);

        Assert.True(settings.Enabled);
        Assert.Equal("test-service", settings.ServiceName);
        Assert.Equal(8081, settings.RemotePort);
        Assert.Equal(8558, settings.ManagementPort);
        Assert.Equal("localhost", settings.PublicHostName);
    }
}
