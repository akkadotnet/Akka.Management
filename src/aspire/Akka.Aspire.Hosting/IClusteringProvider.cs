// -----------------------------------------------------------------------
//  <copyright file="IClusteringProvider.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Aspire.Hosting;

/// <summary>
/// Provides clustering configuration for Akka.NET services in .NET Aspire.
/// </summary>
public interface IClusteringProvider
{
    /// <summary>
    /// Configures the specified resource builder with clustering settings.
    /// </summary>
    /// <typeparam name="T">The resource type that supports environment variables.</typeparam>
    /// <param name="builder">The resource builder to configure.</param>
    void ConfigureResource<T>(IResourceBuilder<T> builder) where T : IResourceWithEnvironment;
}
