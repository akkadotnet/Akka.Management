// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using Akka.Hosting;

namespace Akka.Discovery.Redis
{
    /// <summary>
    /// Extension methods for configuring Redis-based service discovery with Akka.Hosting
    /// </summary>
    public static class AkkaHostingExtensions
    {
        /// <summary>
        /// Adds Akka.Discovery.Redis support to the ActorSystem.
        /// Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        /// a complete solution.
        /// </summary>
        /// <param name="builder">The builder instance being configured</param>
        /// <param name="connectionString">The connection string used to connect to Redis</param>
        /// <param name="serviceName">The service name assigned to the cluster</param>
        /// <returns>The same AkkaConfigurationBuilder instance originally passed in</returns>
        /// <example>
        /// <code>
        /// services.AddAkka("mySystem", builder => {
        ///     builder.WithClusterBootstrap(options =>
        ///     {
        ///         options.ContactPointDiscovery.ServiceName = "testService";
        ///         options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///     }, autoStart: true)
        ///     .WithRedisDiscovery("localhost:6379");
        /// });
        /// </code>
        /// </example>
        public static AkkaConfigurationBuilder WithRedisDiscovery(
            this AkkaConfigurationBuilder builder,
            string connectionString,
            string? serviceName = null)
        {
            var options = new RedisDiscoveryOptions
            {
                ConnectionString = connectionString,
                ServiceName = serviceName
            };
            return builder.WithRedisDiscovery(options);
        }

        /// <summary>
        /// Adds Akka.Discovery.Redis support to the ActorSystem.
        /// Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        /// a complete solution.
        /// </summary>
        /// <param name="builder">The builder instance being configured</param>
        /// <param name="configure">An action that modifies a RedisDiscoveryOptions instance</param>
        /// <returns>The same AkkaConfigurationBuilder instance originally passed in</returns>
        /// <example>
        /// <code>
        /// services.AddAkka("mySystem", builder => {
        ///     builder.WithClusterBootstrap(options =>
        ///     {
        ///         options.ContactPointDiscovery.ServiceName = "testService";
        ///         options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///     }, autoStart: true)
        ///     .WithRedisDiscovery(options => {
        ///         options.ConnectionString = "localhost:6379";
        ///         options.ServiceName = "my-service";
        ///     });
        /// });
        /// </code>
        /// </example>
        public static AkkaConfigurationBuilder WithRedisDiscovery(
            this AkkaConfigurationBuilder builder,
            Action<RedisDiscoveryOptions> configure)
        {
            var options = new RedisDiscoveryOptions();
            configure(options);
            return builder.WithRedisDiscovery(options);
        }

        /// <summary>
        /// Adds Akka.Discovery.Redis support to the ActorSystem.
        /// Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        /// a complete solution.
        /// </summary>
        /// <param name="builder">The builder instance being configured</param>
        /// <param name="options">The RedisDiscoveryOptions instance used to configure the plugin</param>
        /// <returns>The same AkkaConfigurationBuilder instance originally passed in</returns>
        public static AkkaConfigurationBuilder WithRedisDiscovery(
            this AkkaConfigurationBuilder builder,
            RedisDiscoveryOptions options)
        {
            options.Apply(builder);

            // Force start the extension
            builder.AddStartup((system, registry) =>
            {
                RedisDiscovery.Get(system);
            });

            return builder;
        }
    }
}
