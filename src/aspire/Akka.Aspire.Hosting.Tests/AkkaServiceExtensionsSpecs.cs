// -----------------------------------------------------------------------
//  <copyright file="AkkaServiceExtensionsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Akka.Aspire.Hosting.Tests;

/// <summary>
/// Tests for the Akka.NET AppHost-side extensions in .NET Aspire.
/// </summary>
public class AkkaServiceExtensionsSpecs
{
    [Fact]
    public void AddAkka_ShouldCreateAkkaServiceWithCorrectName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var serviceName = "my-akka-service";

        var akkaService = appBuilder.AddAkka(serviceName);

        akkaService.Should().NotBeNull();
        akkaService.Name.Should().Be(serviceName);
        akkaService.Builder.Should().BeSameAs(appBuilder);
        akkaService.Clustering.Should().BeNull();
    }

    [Fact]
    public void AddAkka_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        IDistributedApplicationBuilder builder = null!;

        var act = () => builder.AddAkka("test");

        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void AddAkka_WithNullName_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var act = () => appBuilder.AddAkka(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void WithClustering_ShouldSetClusteringProvider()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");
        var redis = appBuilder.AddRedis("redis");

        var result = akkaService.WithClustering(redis);

        result.Should().BeSameAs(akkaService);
        akkaService.Clustering.Should().NotBeNull();
    }

    [Fact]
    public void WithClustering_WithNullAkkaService_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var redis = appBuilder.AddRedis("redis");
        AkkaService akkaService = null!;

        var act = () => akkaService.WithClustering(redis);

        act.Should().Throw<ArgumentNullException>().WithParameterName("akkaService");
    }

    [Fact]
    public void WithClustering_WithNullResource_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");

        var act = () => akkaService.WithClustering(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("resource");
    }

    [Fact]
    public void WithReference_ShouldConfigureEndpointsAndEnvironmentVariables()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var redis = appBuilder.AddRedis("redis");
        akkaService.WithClustering(redis);

        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        var result = containerResource.WithReference(akkaService);

        result.Should().BeSameAs(containerResource);

        var endpoints = containerResource.Resource.Annotations.OfType<EndpointAnnotation>().ToList();
        endpoints.Should().Contain(e => e.Name == "akka-remote");
        endpoints.Should().Contain(e => e.Name == "akka-management");

        var remoteEndpoint = endpoints.First(e => e.Name == "akka-remote");
        remoteEndpoint.UriScheme.Should().Be("tcp");
        remoteEndpoint.IsProxied.Should().BeTrue();
        remoteEndpoint.IsExternal.Should().BeFalse();

        var managementEndpoint = endpoints.First(e => e.Name == "akka-management");
        managementEndpoint.UriScheme.Should().Be("http");
        managementEndpoint.IsProxied.Should().BeTrue();
        managementEndpoint.IsExternal.Should().BeFalse();

        var envCallbacks = containerResource.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        envCallbacks.Should().NotBeEmpty();
    }

    [Fact]
    public void WithReference_WithoutClustering_ShouldStillConfigureEndpointsAndEnvironment()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        var result = containerResource.WithReference(akkaService);

        result.Should().BeSameAs(containerResource);

        var endpoints = containerResource.Resource.Annotations.OfType<EndpointAnnotation>();
        endpoints.Should().Contain(e => e.Name == "akka-remote");
        endpoints.Should().Contain(e => e.Name == "akka-management");
    }

    [Fact]
    public void WithReference_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");
        IResourceBuilder<ContainerResource> builder = null!;

        var act = () => builder.WithReference(akkaService);

        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void WithReference_WithNullAkkaService_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        var act = () => containerResource.WithReference(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("akkaService");
    }

    [Fact]
    public void WithReference_WithReplicas_ShouldSetCorrectRequiredContactPointsNr()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        containerResource.Resource.Annotations.Add(new ReplicaAnnotation(3));

        containerResource.WithReference(akkaService);

        using var app = appBuilder.Build();
        var distributedAppModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = distributedAppModel.Resources.OfType<ContainerResource>().First();

        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var envContext = new EnvironmentCallbackContext(executionContext);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            callback.Callback(envContext);
        }

        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__RequiredContactPointsNr");
        envContext.EnvironmentVariables["Akka__Cluster__RequiredContactPointsNr"].Should().Be("3");
    }

    [Fact]
    public void WithReference_ShouldSetStandardEnvironmentVariables()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        containerResource.WithReference(akkaService);

        using var app = appBuilder.Build();
        var distributedAppModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = distributedAppModel.Resources.OfType<ContainerResource>().First();

        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var envContext = new EnvironmentCallbackContext(executionContext);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            callback.Callback(envContext);
        }

        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__Enabled");
        envContext.EnvironmentVariables["Akka__Cluster__Enabled"].Should().Be("true");
        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__PublicHostName");
        envContext.EnvironmentVariables["Akka__Cluster__PublicHostName"].Should().Be("localhost");
        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__ServiceName");
        envContext.EnvironmentVariables["Akka__Cluster__ServiceName"].Should().Be("my-akka-cluster");
        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__FilterOnFallbackPort");
        envContext.EnvironmentVariables["Akka__Cluster__FilterOnFallbackPort"].Should().Be("false");
        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__RequiredContactPointsNr");
        envContext.EnvironmentVariables["Akka__Cluster__RequiredContactPointsNr"].Should().Be("1");
    }

    [Fact]
    public void ClusteringProvider_ShouldAddEnvironmentCallbacks()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var redis = appBuilder.AddRedis("redis");
        akkaService.WithClustering(redis);

        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        containerResource.WithReference(akkaService);

        var envCallbacks = containerResource.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        envCallbacks.Should().NotBeEmpty("because clustering provider should add environment callbacks");
    }

    [Fact]
    public async Task ClusteringProvider_ShouldInjectConnectionStringName()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var redis = appBuilder.AddRedis("my-redis");
        akkaService.WithClustering(redis);

        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        containerResource.WithReference(akkaService);

        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var envContext = new EnvironmentCallbackContext(executionContext);
        var envCallbacks = containerResource.Resource.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();

        // The Redis WithReference callback throws during connection string resolution in unit
        // tests; we only need to verify ClusteringProvider's own env vars.
        var tasks = envCallbacks.Select(async cb =>
        {
            try { await cb.Callback(envContext); }
            catch (InvalidOperationException) { /* Redis connection string not resolvable in unit test */ }
        });
        await Task.WhenAll(tasks);

        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__Clustering__ProviderType");
        envContext.EnvironmentVariables["Akka__Cluster__Clustering__ProviderType"].Should().Be("Redis");
        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__Clustering__ConnectionStringName");
        envContext.EnvironmentVariables["Akka__Cluster__Clustering__ConnectionStringName"].Should().Be("my-redis");
    }
}
