// -----------------------------------------------------------------------
//  <copyright file="AkkaAspireBootstrapTestKitSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Cluster;
using Akka.Hosting;
using FluentAssertions;
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
        config.GetString("akka.remote.dot-netty.tcp.hostname").Should().Be("0.0.0.0");
        config.GetString("akka.remote.dot-netty.tcp.public-hostname").Should().Be("test-host");
    }

    [Fact]
    public void Should_use_cluster_actor_provider()
    {
        // Verify cluster is available by accessing the Cluster extension
        var cluster = Cluster.Cluster.Get(Sys);
        cluster.Should().NotBeNull();
        cluster.SelfAddress.Protocol.Should().Be("akka.tcp");
    }

    [Fact]
    public void Should_set_discovery_method_to_config()
    {
        Sys.Settings.Config.GetString("akka.discovery.method").Should().Be("config");
    }

    [Fact]
    public void Should_configure_cluster_bootstrap()
    {
        var config = Sys.Settings.Config;
        config.GetInt("akka.management.cluster.bootstrap.contact-point-discovery.required-contact-point-nr").Should().Be(2);
        config.GetString("akka.management.cluster.bootstrap.contact-point-discovery.service-name").Should().Be("test-service");
    }

    [Fact]
    public void Should_configure_management_endpoint()
    {
        var config = Sys.Settings.Config;
        config.GetString("akka.management.http.hostname").Should().Be("test-host");
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
        provider.Should().NotContain("Cluster");
    }

    [Fact]
    public void Should_not_configure_remote_when_disabled()
    {
        // Default remote port should remain 0 (random) since we didn't configure it
        Sys.Settings.Config.GetInt("akka.remote.dot-netty.tcp.port").Should().Be(0);
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
        roles.Should().Contain("test-role");
        roles.Should().Contain("worker");
    }

    [Fact]
    public void Should_configure_cluster_provider()
    {
        // Verify cluster is available
        var cluster = Cluster.Cluster.Get(Sys);
        cluster.Should().NotBeNull();
        cluster.SelfAddress.Protocol.Should().Be("akka.tcp");
    }
}
