// -----------------------------------------------------------------------
//  <copyright file="AkkaAspireClusterSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Aspire;

/// <summary>
/// Settings for Akka.NET cluster bootstrap in Aspire environments.
/// Binds from the IConfiguration section 'Akka:Cluster'.
/// </summary>
public class AkkaAspireClusterSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether Akka.NET clustering is enabled.
    /// Default is false.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the port for Akka.Remote communication.
    /// Default is 8081.
    /// </summary>
    public int RemotePort { get; set; } = 8081;

    /// <summary>
    /// Gets or sets the port for Akka.Management HTTP endpoint.
    /// Default is 8558.
    /// </summary>
    public int ManagementPort { get; set; } = 8558;

    /// <summary>
    /// Gets or sets the public hostname for the Akka.Remote endpoint.
    /// Default is "localhost".
    /// </summary>
    public string PublicHostName { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the service name for cluster discovery.
    /// Default is "default".
    /// </summary>
    public string ServiceName { get; set; } = "default";

    /// <summary>
    /// Gets or sets the required number of contact points for cluster bootstrap.
    /// Default is 1.
    /// </summary>
    public int RequiredContactPointsNr { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether to filter on the fallback port during discovery.
    /// Default is false.
    /// </summary>
    public bool FilterOnFallbackPort { get; set; } = false;

    /// <summary>
    /// Gets or sets the clustering configuration settings.
    /// </summary>
    public AkkaAspireClusteringSettings? Clustering { get; set; }
}

/// <summary>
/// Settings for Akka.NET clustering provider configuration.
/// </summary>
public class AkkaAspireClusteringSettings
{
    /// <summary>
    /// Gets or sets the provider type for service discovery.
    /// Valid values are: "Redis", "AzureTableStorage", "Kubernetes", "Config".
    /// </summary>
    public string? ProviderType { get; set; }

    /// <summary>
    /// Gets or sets the connection string name for the discovery backend resource.
    /// This is injected by the hosting package and corresponds to the Aspire resource name.
    /// </summary>
    public string? ConnectionStringName { get; set; }
}
