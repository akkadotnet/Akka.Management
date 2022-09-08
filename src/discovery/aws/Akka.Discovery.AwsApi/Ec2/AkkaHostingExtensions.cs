// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Hosting;

namespace Akka.Discovery.AwsApi.Ec2
{
    public static class AkkaHostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Discovery.AwsApi.Ec2 support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder
        ///             .WithClustering()
        ///             .WithClusterBootstrap(setup =>
        ///             {
        ///                 setup.ContactPointDiscovery.ServiceName = "testService";
        ///                 setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///             }, autoStart: true)
        ///             .WithAwsEc2Discovery();
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAwsEc2Discovery(this AkkaConfigurationBuilder builder)
            => builder.WithAwsEc2Discovery(new Ec2ServiceDiscoverySetup());

        /// <summary>
        ///     Adds Akka.Discovery.AwsApi.Ec2 support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies an <see cref="Ec2ServiceDiscoverySetup"/> instance, used
        ///     to configure Akka.Discovery.AwsApi.Ec2.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder
        ///             .WithClustering()
        ///             .WithClusterBootstrap(setup =>
        ///             {
        ///                 setup.ContactPointDiscovery.ServiceName = "testService";
        ///                 setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///             }, autoStart: true)
        ///             .WithAwsEc2Discovery(setup => {
        ///                 setup.WithCredentialProvider&lt;AnonymousEc2CredentialProvider&gt;();
        ///                 setup.TagKey = "myTag";
        ///             });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAwsEc2Discovery(
            this AkkaConfigurationBuilder builder,
            Action<Ec2ServiceDiscoverySetup> configure)
        {
            var setup = new Ec2ServiceDiscoverySetup();
            configure(setup);
            return builder.WithAwsEc2Discovery(setup);
        }

        /// <summary>
        ///     Adds Akka.Discovery.AwsApi.Ec2 support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="setup">
        ///     The <see cref="Ec2ServiceDiscoverySetup"/> instance used to configure Akka.Discovery.Azure.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder
        ///             .WithClustering()
        ///             .WithClusterBootstrap(setup =>
        ///             {
        ///                 setup.ContactPointDiscovery.ServiceName = "testService";
        ///                 setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///             }, autoStart: true)
        ///             .WithAwsEc2Discovery(setup => {
        ///                 setup.WithCredentialProvider&lt;AnonymousEc2CredentialProvider&gt;();
        ///                 setup.TagKey = "myTag";
        ///             });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAwsEc2Discovery(
            this AkkaConfigurationBuilder builder,
            Ec2ServiceDiscoverySetup setup)
        {
            builder.AddHocon("akka.discovery.method = aws-api-ec2-tag-based", HoconAddMode.Prepend);
            builder.AddSetup(setup);
            
            // force start the module
            builder.AddStartup((system, registry) =>
            {
                AwsEc2Discovery.Get(system);
            });
            return builder;
        }
    }
}