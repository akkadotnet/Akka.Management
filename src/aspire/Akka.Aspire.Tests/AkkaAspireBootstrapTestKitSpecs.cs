// -----------------------------------------------------------------------
//  <copyright file="AkkaAspireBootstrapTestKitSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Cluster;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Akka.Aspire.Tests;

/// <summary>
/// TestKit-based integration tests that validate the Akka configuration pipeline through the
/// real Akka.Hosting infrastructure. Uses <c>autoStartBootstrap: false</c> to prevent
/// the cluster bootstrap from auto-starting (which would terminate the system in tests
/// without real peers). Discovery method switching is tested separately in
/// <see cref="AkkaAspireExtensionsSpecs"/>.
/// </summary>
public class ConfigProviderBootstrapSpecs : global::Akka.Hosting.TestKit.TestKit
{
    public ConfigProviderBootstrapSpecs(ITestOutputHelper output)
        : base(nameof(ConfigProviderBootstrapSpecs), output) { }

    protected override void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder builder)
    {
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:RemotePort", "0" },
            { "Akka:Cluster:ManagementPort", "0" },
            { "Akka:Cluster:PublicHostName", "test-host" },
            { "Akka:Cluster:ServiceName", "test-service" },
            { "Akka:Cluster:RequiredContactPointsNr", "2" },
            { "Akka:Cluster:FilterOnFallbackPort", "false" },
            { "Akka:Cluster:Clustering:ProviderType", "Config" }
        });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithAspireClusterBootstrap(provider, autoStartBootstrap: false);
    }

    [Fact]
    public void Should_configure_remote_correctly()
    {
        var config = Sys.Settings.Config;
        Assert.Equal("0.0.0.0", config.GetString("akka.remote.dot-netty.tcp.hostname"));
        Assert.Equal("test-host", config.GetString("akka.remote.dot-netty.tcp.public-hostname"));
    }

    [Fact]
    public void Should_use_cluster_actor_provider()
    {
        // Verify cluster is available by accessing the Cluster extension
        var cluster = Cluster.Cluster.Get(Sys);
        Assert.NotNull(cluster);
        Assert.Equal("akka.tcp", cluster.SelfAddress.Protocol);
    }

    [Fact]
    public void Should_set_discovery_method_to_config()
    {
        Assert.Equal("config", Sys.Settings.Config.GetString("akka.discovery.method"));
    }

    [Fact]
    public void Should_configure_cluster_bootstrap()
    {
        var config = Sys.Settings.Config;
        Assert.Equal(2, config.GetInt("akka.management.cluster.bootstrap.contact-point-discovery.required-contact-point-nr"));
        Assert.Equal("test-service", config.GetString("akka.management.cluster.bootstrap.contact-point-discovery.service-name"));
    }

    [Fact]
    public void Should_configure_management_endpoint()
    {
        var config = Sys.Settings.Config;
        Assert.Equal("test-host", config.GetString("akka.management.http.hostname"));
    }
}

public class DisabledBootstrapSpecs : global::Akka.Hosting.TestKit.TestKit
{
    public DisabledBootstrapSpecs(ITestOutputHelper output)
        : base(nameof(DisabledBootstrapSpecs), output) { }

    protected override void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder builder)
    {
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "false" }
        });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithAspireClusterBootstrap(provider);
    }

    [Fact]
    public void Should_not_set_cluster_provider_when_disabled()
    {
        // When disabled, the provider should not be cluster
        var provider = Sys.Settings.Config.GetString("akka.actor.provider");
        Assert.DoesNotContain("Cluster", provider);
    }

    [Fact]
    public void Should_not_configure_remote_when_disabled()
    {
        // Default remote port should remain 0 (random) since we didn't configure it
        Assert.Equal(0, Sys.Settings.Config.GetInt("akka.remote.dot-netty.tcp.port"));
    }
}

public class CustomClusterOptionsSpecs : global::Akka.Hosting.TestKit.TestKit
{
    public CustomClusterOptionsSpecs(ITestOutputHelper output)
        : base(nameof(CustomClusterOptionsSpecs), output) { }

    protected override void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder builder)
    {
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:RemotePort", "0" },
            { "Akka:Cluster:ManagementPort", "0" },
            { "Akka:Cluster:PublicHostName", "localhost" },
            { "Akka:Cluster:ServiceName", "test-service" },
            { "Akka:Cluster:RequiredContactPointsNr", "1" },
            { "Akka:Cluster:Clustering:ProviderType", "Config" }
        });
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.WithAspireClusterBootstrap(provider,
            clusterConfigure: cluster =>
            {
                cluster.Roles = ["test-role", "worker"];
            }, autoStartBootstrap: false);
    }

    [Fact]
    public void Should_apply_custom_roles()
    {
        var roles = Sys.Settings.Config.GetStringList("akka.cluster.roles");
        Assert.Contains("test-role", roles);
        Assert.Contains("worker", roles);
    }

    [Fact]
    public void Should_configure_cluster_provider()
    {
        // Verify cluster is available
        var cluster = Cluster.Cluster.Get(Sys);
        Assert.NotNull(cluster);
        Assert.Equal("akka.tcp", cluster.SelfAddress.Protocol);
    }
}
