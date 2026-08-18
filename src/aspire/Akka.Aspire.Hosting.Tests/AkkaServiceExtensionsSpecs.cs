// -----------------------------------------------------------------------
//  <copyright file="AkkaServiceExtensionsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
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

        Assert.NotNull(akkaService);
        Assert.Equal(serviceName, akkaService.Name);
        Assert.Same(appBuilder, akkaService.Builder);
        Assert.Null(akkaService.Clustering);
    }

    [Fact]
    public void AddAkka_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        IDistributedApplicationBuilder builder = null!;

        var act = () => builder.AddAkka("test");

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("builder", ex.ParamName);
    }

    [Fact]
    public void AddAkka_WithNullName_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var act = () => appBuilder.AddAkka(null!);

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void WithClustering_ShouldSetClusteringProvider()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");
        var redis = appBuilder.AddRedis("redis");

        var result = akkaService.WithClustering(redis);

        Assert.Same(akkaService, result);
        Assert.NotNull(akkaService.Clustering);
    }

    [Fact]
    public void WithClustering_WithNullAkkaService_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var redis = appBuilder.AddRedis("redis");
        AkkaService akkaService = null!;

        var act = () => akkaService.WithClustering(redis);

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("akkaService", ex.ParamName);
    }

    [Fact]
    public void WithClustering_WithNullResource_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");

        var act = () => akkaService.WithClustering(null!);

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("resource", ex.ParamName);
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

        Assert.Same(containerResource, result);

        var endpoints = containerResource.Resource.Annotations.OfType<EndpointAnnotation>().ToList();
        Assert.Contains(endpoints, e => e.Name == "akka-remote");
        Assert.Contains(endpoints, e => e.Name == "akka-management");

        var remoteEndpoint = endpoints.First(e => e.Name == "akka-remote");
        Assert.Equal("tcp", remoteEndpoint.UriScheme);
        Assert.True(remoteEndpoint.IsProxied);
        Assert.False(remoteEndpoint.IsExternal);

        var managementEndpoint = endpoints.First(e => e.Name == "akka-management");
        Assert.Equal("http", managementEndpoint.UriScheme);
        Assert.True(managementEndpoint.IsProxied);
        Assert.False(managementEndpoint.IsExternal);

        var envCallbacks = containerResource.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envCallbacks);
    }

    [Fact]
    public void WithReference_WithoutClustering_ShouldStillConfigureEndpointsAndEnvironment()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        var result = containerResource.WithReference(akkaService);

        Assert.Same(containerResource, result);

        var endpoints = containerResource.Resource.Annotations.OfType<EndpointAnnotation>();
        Assert.Contains(endpoints, e => e.Name == "akka-remote");
        Assert.Contains(endpoints, e => e.Name == "akka-management");
    }

    [Fact]
    public void WithReference_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");
        IResourceBuilder<ContainerResource> builder = null!;

        var act = () => builder.WithReference(akkaService);

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("builder", ex.ParamName);
    }

    [Fact]
    public void WithReference_WithNullAkkaService_ShouldThrowArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        var act = () => containerResource.WithReference(null!);

        var ex = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("akkaService", ex.ParamName);
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

        Assert.Contains("Akka__Cluster__RequiredContactPointsNr", envContext.EnvironmentVariables.Keys);
        Assert.Equal("3", envContext.EnvironmentVariables["Akka__Cluster__RequiredContactPointsNr"]);
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

        Assert.Contains("Akka__Cluster__Enabled", envContext.EnvironmentVariables.Keys);
        Assert.Equal("true", envContext.EnvironmentVariables["Akka__Cluster__Enabled"]);
        Assert.Contains("Akka__Cluster__PublicHostName", envContext.EnvironmentVariables.Keys);
        Assert.Equal("localhost", envContext.EnvironmentVariables["Akka__Cluster__PublicHostName"]);
        Assert.Contains("Akka__Cluster__ServiceName", envContext.EnvironmentVariables.Keys);
        Assert.Equal("my-akka-cluster", envContext.EnvironmentVariables["Akka__Cluster__ServiceName"]);
        Assert.Contains("Akka__Cluster__FilterOnFallbackPort", envContext.EnvironmentVariables.Keys);
        Assert.Equal("false", envContext.EnvironmentVariables["Akka__Cluster__FilterOnFallbackPort"]);
        Assert.Contains("Akka__Cluster__RequiredContactPointsNr", envContext.EnvironmentVariables.Keys);
        Assert.Equal("1", envContext.EnvironmentVariables["Akka__Cluster__RequiredContactPointsNr"]);
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
        Assert.True(envCallbacks.Any(), "because clustering provider should add environment callbacks");
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

        Assert.Contains("Akka__Cluster__Clustering__ProviderType", envContext.EnvironmentVariables.Keys);
        Assert.Equal("Redis", envContext.EnvironmentVariables["Akka__Cluster__Clustering__ProviderType"]);
        Assert.Contains("Akka__Cluster__Clustering__ConnectionStringName", envContext.EnvironmentVariables.Keys);
        Assert.Equal("my-redis", envContext.EnvironmentVariables["Akka__Cluster__Clustering__ConnectionStringName"]);
    }
}
