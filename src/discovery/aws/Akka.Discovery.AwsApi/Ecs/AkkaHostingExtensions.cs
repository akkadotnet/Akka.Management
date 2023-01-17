// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Hosting;
using Amazon.ECS.Model;

namespace Akka.Discovery.AwsApi.Ecs
{
    public static class AkkaHostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Discovery.AwsApi.Ecs support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="clusterName">
        ///     <para>
        ///         Optional. The name of the AWS ECS cluster.
        ///     </para>
        ///     <b>Default</b>: "default"
        /// </param>
        /// <param name="tags">
        ///     <para>
        ///     Optional. A list of <see cref="Tag"/> used to filter the ECS cluster tasks.
        ///     The task must have the same exact list of tags to be considered as potential contact point by the
        ///     discovery module.
        ///     </para>
        ///     <b>Default</b>: empty list.
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
        ///                 options.ContactPointDiscovery.ServiceName = "testService";
        ///                 options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///             }, autoStart: true)
        ///             .WithAwsEcsDiscovery();
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAwsEcsDiscovery(
            this AkkaConfigurationBuilder builder,
            string? clusterName = null,
            IEnumerable<Tag>? tags = null)
            => builder.WithAwsEcsDiscovery(new EcsServiceDiscoveryOptions
            {
                Cluster = clusterName,
                Tags = tags
            });

        /// <summary>
        ///     Adds Akka.Discovery.AwsApi.Ec2 support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies an <see cref="EcsServiceDiscoveryOptions"/> instance, used
        ///     to configure Akka.Discovery.AwsApi.Ecs.
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
        ///                 options.ContactPointDiscovery.ServiceName = "testService";
        ///                 options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///             }, autoStart: true)
        ///             .WithAwsEcsDiscovery(options => {
        ///                 options.Cluster = "my-ecs-cluster-name";
        ///             });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAwsEcsDiscovery(
            this AkkaConfigurationBuilder builder,
            Action<EcsServiceDiscoveryOptions> configure)
        {
            var setup = new EcsServiceDiscoveryOptions();
            configure(setup);
            return builder.WithAwsEcsDiscovery(setup);
        }

        /// <summary>
        ///     Adds Akka.Discovery.AwsApi.Ec2 support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="options">
        ///     The <see cref="EcsServiceDiscoveryOptions"/> instance used to configure Akka.Discovery.AwsApi.Ecs.
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
        ///                 options.ContactPointDiscovery.ServiceName = "testService";
        ///                 options.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///             }, autoStart: true)
        ///             .WithAwsEcsDiscovery(new EcsServiceDiscoveryOptions {
        ///                 Cluster = "my-ecs-cluster-name"
        ///             });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAwsEcsDiscovery(
            this AkkaConfigurationBuilder builder,
            EcsServiceDiscoveryOptions options)
        {
            builder.AddHocon($"akka.discovery.method = {options.ConfigPath}", HoconAddMode.Prepend);
            options.Apply(builder);
            builder.AddHocon(AwsEcsDiscovery.DefaultConfiguration(), HoconAddMode.Append);
            
            // force start the module
            builder.AddStartup((system, registry) =>
            {
                AwsEcsDiscovery.Get(system);
            });
            return builder;
        }

    }
}