// -----------------------------------------------------------------------
//  <copyright file="AkkaService.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Aspire.Hosting;

/// <summary>
/// Represents an Akka.NET service configuration in .NET Aspire.
/// This is a configuration holder, not a resource.
/// </summary>
public sealed class AkkaService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AkkaService"/> class.
    /// </summary>
    /// <param name="name">The name of the Akka.NET service.</param>
    /// <param name="builder">The distributed application builder.</param>
    public AkkaService(string name, IDistributedApplicationBuilder builder)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>
    /// Gets the name of the Akka.NET service.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the distributed application builder.
    /// </summary>
    public IDistributedApplicationBuilder Builder { get; }

    /// <summary>
    /// Gets or sets the clustering provider for this Akka.NET service.
    /// </summary>
    public IClusteringProvider? Clustering { get; set; }
}
