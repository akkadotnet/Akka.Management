// -----------------------------------------------------------------------
//  <copyright file="AkkaAspireExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Akka.Aspire;

/// <summary>
/// Extension methods for configuring Akka.NET with Aspire cluster bootstrap.
/// </summary>
public static class AkkaAspireExtensions
{
    /// <summary>
    /// Configures Akka.NET with Aspire cluster bootstrap settings.
    /// Reads configuration from the 'Akka:Cluster' section and sets up Akka.Remote,
    /// Akka.Cluster, Akka.Management, and Cluster Bootstrap when enabled.
    /// </summary>
    /// <param name="builder">The Akka configuration builder.</param>
    /// <param name="sp">The service provider for accessing IConfiguration.</param>
    /// <param name="configureDiscovery">Optional callback to configure the service discovery plugin.
    /// Receives the Akka configuration builder and the application's <see cref="IConfiguration"/>.
    /// Use this to call discovery extension methods like <c>WithRedisDiscovery</c> or <c>WithAzureDiscovery</c>.
    /// When provided, the discovery plugin's <c>IsDefaultPlugin = true</c> will set <c>akka.discovery.method</c> automatically.</param>
    /// <param name="clusterConfigure">Optional callback to customize cluster options.</param>
    /// <param name="autoStartBootstrap">Whether to automatically start Cluster Bootstrap on actor system startup.
    /// Set to false for testing scenarios where you want to control bootstrap lifecycle manually.</param>
    /// <returns>The Akka configuration builder for method chaining.</returns>
    public static AkkaConfigurationBuilder WithAspireClusterBootstrap(
        this AkkaConfigurationBuilder builder,
        IServiceProvider sp,
        Action<AkkaConfigurationBuilder, IConfiguration>? configureDiscovery = null,
        Action<ClusterOptions>? clusterConfigure = null,
        bool autoStartBootstrap = true)
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var settings = new AkkaAspireClusterSettings();
        configuration.GetSection("Akka:Cluster").Bind(settings);

        // If clustering is not enabled, return immediately without configuration
        if (!settings.Enabled)
        {
            return builder;
        }

        // Configure Akka.Remote
        builder.WithRemoting(
            hostname: "0.0.0.0",
            port: settings.RemotePort,
            publicHostname: settings.PublicHostName,
            publicPort: settings.RemotePort);

        // Configure Akka.Cluster with empty seed nodes (bootstrap will handle discovery)
        var clusterOptions = new ClusterOptions
        {
            SeedNodes = Array.Empty<string>()
        };
        clusterConfigure?.Invoke(clusterOptions);
        builder.WithClustering(clusterOptions);

        // Configure Akka.Management HTTP endpoint
        // hostName must match the discovery target hostname (e.g. "localhost") so the
        // SelfAwareJoinDecider can identify this node in the discovered contact points.
        // bindHostname stays "0.0.0.0" to accept connections on all interfaces.
        builder.WithAkkaManagement(
            hostName: settings.PublicHostName,
            port: settings.ManagementPort,
            bindHostname: "0.0.0.0",
            bindPort: settings.ManagementPort);

        // Configure Cluster Bootstrap
        builder.WithClusterBootstrap(options =>
        {
            options.ContactPointDiscovery.ServiceName = settings.ServiceName;
            options.ContactPointDiscovery.RequiredContactPointsNr = settings.RequiredContactPointsNr;
            options.ContactPointDiscovery.StableMargin = TimeSpan.FromSeconds(5);
            options.ContactPoint.FilterOnFallbackPort = settings.FilterOnFallbackPort;
        }, autoStart: autoStartBootstrap);

        // Let the caller configure their discovery plugin
        configureDiscovery?.Invoke(builder, configuration);

        // Determine the discovery method for HOCON injection
        var discoveryMethod = DetermineDiscoveryMethod(settings.Clustering?.ProviderType);

        // If no configureDiscovery callback was provided, set akka.discovery.method via HOCON
        // (backward compat for manual HOCON-based discovery or auto-detected provider type).
        // When a callback is provided, the discovery plugin's With*Discovery() call sets this
        // via IsDefaultPlugin = true.
        if (configureDiscovery is null)
        {
            builder.AddHocon($"akka.discovery.method = \"{discoveryMethod}\"", HoconAddMode.Prepend);
        }

        // Inject the management port and hostname into the discovery plugin's config
        // so each replica registers with its own unique (hostname, port) tuple.
        // This override layer must win over whatever the plugin sets.
        if (discoveryMethod != "config")
        {
            builder.AddHocon(
                $"akka.discovery.{discoveryMethod}.public-port = {settings.ManagementPort}",
                HoconAddMode.Prepend);
            builder.AddHocon(
                $"akka.discovery.{discoveryMethod}.public-hostname = \"{settings.PublicHostName}\"",
                HoconAddMode.Prepend);
        }

        // Add health checks
        builder.WithActorSystemLivenessCheck();
        builder.WithClusterMembershipCheck();

        return builder;
    }

    /// <summary>
    /// Adds a health check that reports the cluster membership status of this node.
    /// Reports Healthy when Up, Degraded when Joining/WeaklyUp, Unhealthy otherwise.
    /// </summary>
    /// <param name="builder">The Akka configuration builder.</param>
    /// <param name="failureStatus">The health status to report on failure.</param>
    /// <param name="tags">Optional tags for the health check.</param>
    /// <returns>The Akka configuration builder for method chaining.</returns>
    public static AkkaConfigurationBuilder WithClusterMembershipCheck(
        this AkkaConfigurationBuilder builder,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        // Cluster membership is the natural readiness signal: a node is only "ready" to serve traffic
        // once it has joined the cluster (MemberStatus.Up). Tagging it "readiness" lets a /healthz/ready
        // probe gate on it. Without this the readiness endpoint would match no checks and be always-green.
        tags ??= new[] { "readiness" };

        return builder.WithHealthCheck("akka-cluster-membership", (system, _, _) =>
        {
            var cluster = Cluster.Cluster.Get(system);
            var status = cluster.SelfMember.Status;

            if (status == MemberStatus.Up)
                return Task.FromResult(HealthCheckResult.Healthy($"Cluster member status: {status}"));

            if (status is MemberStatus.Joining or MemberStatus.WeaklyUp)
                return Task.FromResult(HealthCheckResult.Degraded($"Cluster member status: {status}"));

            return Task.FromResult(HealthCheckResult.Unhealthy($"Cluster member status: {status}"));
        }, failureStatus, tags);
    }

    private static string DetermineDiscoveryMethod(string? providerType)
    {
        if (string.IsNullOrEmpty(providerType))
        {
            return "config";
        }

        return providerType.ToLowerInvariant() switch
        {
            "redis" => "redis",
            "azuretablestorage" => "azure",
            "kubernetes" => "kubernetes-api",
            "config" => "config",
            _ => "config"
        };
    }
}
