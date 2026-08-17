// -----------------------------------------------------------------------
//  <copyright file="ClusteringProvider.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Aspire.Hosting;

/// <summary>
/// Internal implementation of <see cref="IClusteringProvider"/> that auto-detects the provider type
/// from the resource type name and configures connection strings and environment variables.
/// </summary>
internal sealed class ClusteringProvider : IClusteringProvider
{
    private readonly IResourceBuilder<IResourceWithConnectionString> _resource;
    private readonly string _providerType;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClusteringProvider"/> class.
    /// </summary>
    /// <param name="resource">The resource builder that provides the connection string.</param>
    public ClusteringProvider(IResourceBuilder<IResourceWithConnectionString> resource)
    {
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));

        // Auto-detect provider type by stripping "Resource" suffix from the resource type name
        // e.g., "RedisResource" -> "Redis", "PostgresServerResource" -> "PostgresServer"
        var resourceTypeName = resource.Resource.GetType().Name;
        _providerType = resourceTypeName.EndsWith("Resource", StringComparison.Ordinal)
            ? resourceTypeName[..^"Resource".Length]
            : resourceTypeName;
    }

    /// <summary>
    /// Configures the specified resource builder with clustering settings.
    /// </summary>
    /// <typeparam name="T">The resource type that supports environment variables.</typeparam>
    /// <param name="builder">The resource builder to configure.</param>
    public void ConfigureResource<T>(IResourceBuilder<T> builder) where T : IResourceWithEnvironment
    {
        // Inject the connection string reference
        builder.WithReference(_resource);

        // Set clustering provider metadata via environment callback
        // (uses the same callback pattern as AkkaServiceExtensions.WithReference for consistency)
        var providerType = _providerType;
        var connectionStringName = _resource.Resource.Name;
        builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["Akka__Cluster__Clustering__ProviderType"] = providerType;
            context.EnvironmentVariables["Akka__Cluster__Clustering__ConnectionStringName"] = connectionStringName;
        });
    }
}
