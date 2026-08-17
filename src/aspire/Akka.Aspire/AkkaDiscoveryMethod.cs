// -----------------------------------------------------------------------
//  <copyright file="AkkaDiscoveryMethod.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Aspire;

/// <summary>
/// Defines the discovery methods available for Akka.NET cluster bootstrap.
/// </summary>
public enum AkkaDiscoveryMethod
{
    /// <summary>
    /// Use Redis for service discovery.
    /// </summary>
    Redis,

    /// <summary>
    /// Use Azure Table Storage for service discovery.
    /// </summary>
    AzureTableStorage,

    /// <summary>
    /// Use Kubernetes API for service discovery.
    /// </summary>
    Kubernetes,

    /// <summary>
    /// Use configuration-based service discovery.
    /// </summary>
    Config
}
