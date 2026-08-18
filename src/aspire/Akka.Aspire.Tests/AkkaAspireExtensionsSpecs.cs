// -----------------------------------------------------------------------
//  <copyright file="AkkaAspireExtensionsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Akka.Aspire.Tests;

public class AkkaAspireExtensionsSpecs
{
    [Fact]
    public void WithAspireClusterBootstrap_WhenDisabled_ShouldBeNoOp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Akka:Cluster:Enabled", "false" } })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        var result = builder.WithAspireClusterBootstrap(sp);

        Assert.NotNull(result);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithRedisProvider_ShouldConfigureCorrectHocon()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Akka:Cluster:Enabled", "true" },
                { "Akka:Cluster:RemotePort", "8081" },
                { "Akka:Cluster:ManagementPort", "8558" },
                { "Akka:Cluster:PublicHostName", "localhost" },
                { "Akka:Cluster:ServiceName", "test-service" },
                { "Akka:Cluster:RequiredContactPointsNr", "2" },
                { "Akka:Cluster:FilterOnFallbackPort", "false" },
                { "Akka:Cluster:Clustering:ProviderType", "Redis" }
            })
            .Build();

        using var host = new HostBuilder()
            .ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton<IConfiguration>(configuration);
                serviceCollection.AddAkka("TestSystem", (akkaBuilder, provider) =>
                {
                    akkaBuilder.WithAspireClusterBootstrap(provider);
                });
            })
            .Build();

        var actorSystem = host.Services.GetRequiredService<ActorSystem>();
        var config = actorSystem.Settings.Config;

        Assert.Equal("redis", config.GetString("akka.discovery.method"));

        Assert.Equal("0.0.0.0", config.GetString("akka.remote.dot-netty.tcp.hostname"));
        Assert.Equal(8081, config.GetInt("akka.remote.dot-netty.tcp.port"));
        Assert.Equal("localhost", config.GetString("akka.remote.dot-netty.tcp.public-hostname"));
        Assert.Equal(8081, config.GetInt("akka.remote.dot-netty.tcp.public-port"));

        Assert.Equal("localhost", config.GetString("akka.management.http.hostname"));
        Assert.Equal(8558, config.GetInt("akka.management.http.port"));

        Assert.Equal(2, config.GetInt("akka.management.cluster.bootstrap.contact-point-discovery.required-contact-point-nr"));
        Assert.Equal("test-service", config.GetString("akka.management.cluster.bootstrap.contact-point-discovery.service-name"));
        Assert.False(config.GetBoolean("akka.management.cluster.bootstrap.contact-point.filter-on-fallback-port"));

        Assert.Equal("localhost", config.GetString("akka.discovery.redis.public-hostname"));
        Assert.Equal(8558, config.GetInt("akka.discovery.redis.public-port"));
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithAzureProvider_ShouldSetAzureDiscoveryMethod()
        => AssertDiscoveryMethod("AzureTableStorage", "azure");

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithKubernetesProvider_ShouldSetKubernetesDiscoveryMethod()
        => AssertDiscoveryMethod("Kubernetes", "kubernetes-api");

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithNoProvider_ShouldDefaultToConfig()
        => AssertDiscoveryMethod(null, "config");

    [Fact]
    public void WithAspireClusterBootstrap_WithNoDiscoveryCallback_ShouldStillSetDiscoveryMethod()
        => AssertDiscoveryMethod("Redis", "redis");

    private static void AssertDiscoveryMethod(string? providerType, string expectedMethod)
    {
        var dict = new Dictionary<string, string?> { { "Akka:Cluster:Enabled", "true" } };
        if (providerType is not null)
            dict["Akka:Cluster:Clustering:ProviderType"] = providerType;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        using var host = new HostBuilder()
            .ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton<IConfiguration>(configuration);
                serviceCollection.AddAkka("TestSystem", (akkaBuilder, provider) =>
                {
                    akkaBuilder.WithAspireClusterBootstrap(provider);
                });
            })
            .Build();

        var actorSystem = host.Services.GetRequiredService<ActorSystem>();
        Assert.Equal(expectedMethod, actorSystem.Settings.Config.GetString("akka.discovery.method"));
    }

    [Fact]
    public void WithAspireClusterBootstrap_WithCustomClusterConfigure_ShouldApplyCallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Akka:Cluster:Enabled", "true" } })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        var callbackInvoked = false;

        builder.WithAspireClusterBootstrap(sp, clusterConfigure: clusterOptions =>
        {
            callbackInvoked = true;
            clusterOptions.Roles = ["test-role"];
        });

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void WithAspireClusterBootstrap_WithDiscoveryCallback_ShouldInvokeCallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Akka:Cluster:Enabled", "true" },
                { "Akka:Cluster:Clustering:ProviderType", "Redis" },
                { "ConnectionStrings:akka-discovery", "localhost:6379" }
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        var callbackInvoked = false;
        IConfiguration? receivedConfig = null;

        builder.WithAspireClusterBootstrap(sp,
            configureDiscovery: (b, config) =>
            {
                callbackInvoked = true;
                receivedConfig = config;
            });

        Assert.True(callbackInvoked);
        Assert.NotNull(receivedConfig);
        Assert.Equal("localhost:6379", receivedConfig!.GetConnectionString("akka-discovery"));
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenDisabled_ShouldNotInvokeDiscoveryCallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Akka:Cluster:Enabled", "false" } })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        var callbackInvoked = false;

        builder.WithAspireClusterBootstrap(sp,
            configureDiscovery: (b, config) => { callbackInvoked = true; });

        Assert.False(callbackInvoked);
    }
}
