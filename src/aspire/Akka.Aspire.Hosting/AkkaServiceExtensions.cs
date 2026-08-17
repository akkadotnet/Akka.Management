// -----------------------------------------------------------------------
//  <copyright file="AkkaServiceExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Net.Sockets;

namespace Akka.Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Akka.NET services in .NET Aspire.
/// </summary>
public static class AkkaServiceExtensions
{
    /// <summary>
    /// Adds an Akka.NET service configuration to the distributed application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Akka.NET service.</param>
    /// <returns>An <see cref="AkkaService"/> instance for further configuration.</returns>
    public static AkkaService AddAkka(this IDistributedApplicationBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        return new AkkaService(name, builder);
    }

    /// <summary>
    /// Configures clustering for the Akka.NET service using the specified resource.
    /// </summary>
    /// <param name="akkaService">The Akka.NET service to configure.</param>
    /// <param name="resource">The resource that provides the connection string for clustering.</param>
    /// <returns>The <see cref="AkkaService"/> instance for further configuration.</returns>
    public static AkkaService WithClustering(
        this AkkaService akkaService,
        IResourceBuilder<IResourceWithConnectionString> resource)
    {
        ArgumentNullException.ThrowIfNull(akkaService);
        ArgumentNullException.ThrowIfNull(resource);

        akkaService.Clustering = new ClusteringProvider(resource);
        return akkaService;
    }

    /// <summary>
    /// Configures a resource to use the specified Akka.NET service, setting up endpoints and environment variables.
    /// </summary>
    /// <typeparam name="T">The resource type that supports environment variables and endpoints.</typeparam>
    /// <param name="builder">The resource builder to configure.</param>
    /// <param name="akkaService">The Akka.NET service configuration to apply.</param>
    /// <returns>The resource builder for further configuration.</returns>
    public static IResourceBuilder<T> WithReference<T>(
        this IResourceBuilder<T> builder,
        AkkaService akkaService)
        where T : IResourceWithEnvironment, IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(akkaService);

        // Apply clustering configuration if available
        akkaService.Clustering?.ConfigureResource(builder);

        // Add TCP endpoint for Akka.Remote
        builder.WithEndpoint(
            name: "akka-remote",
            scheme: "tcp",
            env: "Akka__Cluster__RemotePort",
            isProxied: true,
            isExternal: false,
            protocol: ProtocolType.Tcp);

        // Add HTTP endpoint for Akka.Management (used by cluster bootstrap for contact point probing)
        builder.WithEndpoint(
            name: "akka-management",
            scheme: "http",
            env: "Akka__Cluster__ManagementPort",
            isProxied: true,
            isExternal: false);

        // Configure environment variables
        builder.WithEnvironment(context =>
        {
            // Detect replica count from annotations
            var replicaCount = "1";
            var replicaAnnotation = builder.Resource.Annotations
                .OfType<ReplicaAnnotation>()
                .FirstOrDefault();

            if (replicaAnnotation != null)
            {
                replicaCount = replicaAnnotation.Replicas.ToString();
            }

            context.EnvironmentVariables["Akka__Cluster__Enabled"] = "true";
            context.EnvironmentVariables["Akka__Cluster__PublicHostName"] = "localhost";
            context.EnvironmentVariables["Akka__Cluster__ServiceName"] = akkaService.Name;
            context.EnvironmentVariables["Akka__Cluster__FilterOnFallbackPort"] = "false";
            context.EnvironmentVariables["Akka__Cluster__RequiredContactPointsNr"] = replicaCount;
        });

        return builder;
    }
}
