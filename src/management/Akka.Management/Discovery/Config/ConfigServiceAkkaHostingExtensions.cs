// -----------------------------------------------------------------------
//  <copyright file="ConfigDiscoveryHostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Hosting;

// ReSharper disable once CheckNamespace
namespace Akka.Discovery.Config.Hosting;

public static class ConfigServiceAkkaHostingExtensions
{
    /// <summary>
    ///     Adds Akka.Discovery.Config.Hosting support to the <see cref="ActorSystem"/>.
    ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
    ///     a complete solution.
    /// </summary>
    /// <param name="builder">
    ///     The builder instance being configured.
    /// </param>
    /// <param name="configure">
    ///     An action that modifies an <see cref="ConfigServiceDiscoveryOptions"/> instance, used
    ///     to configure Akka.Discovery.Config.Hosting.
    /// </param>
    /// <returns>
    ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
    /// </returns>
    /// <example>
    ///   <code>
    ///     services.AddAkka("mySystem", builder => {
    ///         builder
    ///             .WithClustering()
    ///             .WithClusterBootstrap(options =>
    ///             {
    ///                 options.ContactPointDiscovery.ServiceName = "configService";
    ///                 options.ContactPointDiscovery.RequiredContactPointsNr = 2;
    ///                 // NOTE: this is needed to prevent cluster bootstrap from filtering out
    ///                 //       multiple result from a single domain name. The name does not matter.
    ///                 options.ContactPointDiscovery.PortName = "configPort";
    ///             }, autoStart: true)
    ///             .WithConfigDiscovery(options => {
    ///                 opt.Services.Add(new Service
    ///                 {
    ///                     Name = "configService",
    ///                     Endpoints = new [] { "localhost:9999", "localhost:10005" }
    ///                 });
    ///             });
    ///     }
    ///   </code>
    /// </example>
    public static AkkaConfigurationBuilder WithConfigDiscovery(
        this AkkaConfigurationBuilder builder,
        Action<ConfigServiceDiscoveryOptions> configure)
    {
        var options = new ConfigServiceDiscoveryOptions();
        configure(options);
        return builder.WithConfigDiscovery(options);
    }
    
    /// <summary>
    ///     Adds Akka.Discovery.Config.Hosting support to the <see cref="ActorSystem"/>.
    ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
    ///     a complete solution.
    /// </summary>
    /// <param name="builder">
    ///     The builder instance being configured.
    /// </param>
    /// <param name="options">
    ///     The <see cref="ConfigServiceDiscoveryOptions"/> instance used to configure Akka.Discovery.Azure.
    /// </param>
    /// <returns>
    ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
    /// </returns>
    /// <example>
    ///   <code>
    ///     services.AddAkka("mySystem", builder => {
    ///         builder
    ///             .WithClustering()
    ///             .WithClusterBootstrap(options =>
    ///             {
    ///                 options.ContactPointDiscovery.ServiceName = "configService";
    ///                 options.ContactPointDiscovery.RequiredContactPointsNr = 2;
    ///                 // NOTE: this is needed to prevent cluster bootstrap from filtering out
    ///                 //       multiple result from a single domain name. The name does not matter.
    ///                 options.ContactPointDiscovery.PortName = "configPort";
    ///             }, autoStart: true)
    ///             .WithConfigDiscovery(new ConfigServiceDiscoveryOptions
    ///             {
    ///                 Services = new List&lt;Service&gt; {
    ///                     new Service
    ///                     {
    ///                         Name = "configService",
    ///                         Endpoints = new [] { "localhost:9999", "localhost:10005" }
    ///                     }
    ///                 }
    ///             });
    ///     }
    ///   </code>
    /// </example>
    public static AkkaConfigurationBuilder WithConfigDiscovery(
        this AkkaConfigurationBuilder builder,
        ConfigServiceDiscoveryOptions options)
    {
        options.Apply(builder);
        return builder;
    }
}