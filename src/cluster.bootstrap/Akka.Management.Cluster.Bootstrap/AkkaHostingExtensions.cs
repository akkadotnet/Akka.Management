// -----------------------------------------------------------------------
//  <copyright file="ClusterBootstrapExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Hosting;

namespace Akka.Management.Cluster.Bootstrap
{
    public static class AkkaHostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Management.Cluster.Bootstrap support to the <see cref="ActorSystem"/>.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="serviceName">
        ///     Define this name to be looked up in service discovery for "neighboring" nodes
        ///     If undefined, the name will be taken from the AKKA__CLUSTER__BOOTSTRAP__SERVICE_NAME
        ///     environment variable or extracted from the ActorSystem name
        /// </param>
        /// <param name="serviceNamespace">
        ///     Added as suffix to the service-name to build the effective-service name used in the contact-point
        ///     service lookups If undefined, nothing will be appended to the service-name.
        /// 
        ///     Examples, set this to:
        ///     "default.svc.cluster.local" or "my-namespace.svc.cluster.local" for kubernetes clusters.
        /// </param>
        /// <param name="portName">
        ///     The portName passed to discovery. This should be set to the name of the port for Akka Management
        /// </param>
        /// <param name="requiredContactPoints">
        ///     The smallest number of contact points that need to be discovered before the bootstrap process can start.
        ///     For optimal safety during cluster formation, you may want to set these value to the number of initial
        ///     nodes that you know will participate in the cluster (e.g. the value of `spec.replicas` as set in your kubernetes config.
        /// </param>
        /// <param name="newClusterEnabled">
        /// <para>
        ///     Cluster Bootstrap will always attempt to join an existing cluster if possible. However
        ///     if no contact point advertises any seed-nodes a new cluster will be formed by the
        ///     node with the lowest address as decided by the <see cref="LowestAddressJoinDecider"/>.
        /// </para>
        /// <para>
        ///     Setting NewClusterEnabled to false after an initial cluster has formed is recommended to prevent new
        ///     clusters forming during a network partition when nodes are redeployed or restarted.
        /// </para>
        /// </param>
        /// <param name="autoStart">
        ///     When set to true, cluster bootstrap will be started automatically on <see cref="ActorSystem"/> startup.
        ///     Note that this will also automatically start Akka.Management on startup.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example >
        ///     Starting Cluster.Bootstrap manually:
        ///     <code>
        ///         builder.WithClusterBootstrap(autoStart: false);
        ///         builder.AddStartup(async (system, registry) =>
        ///         {
        ///             await AkkaManagement.Get(system).Start();
        ///             await ClusterBootstrap.Get(system).Start();
        ///         });
        ///     </code>
        /// </example>
        public static AkkaConfigurationBuilder WithClusterBootstrap(
            this AkkaConfigurationBuilder builder,
            string serviceName = null,
            string serviceNamespace = null,
            string portName = null,
            int? requiredContactPoints = null,
            bool? newClusterEnabled = null,
            bool autoStart = true)
            => builder.WithClusterBootstrap(new ClusterBootstrapSetup
            {
                NewClusterEnabled = newClusterEnabled,
                ContactPointDiscovery = new ContactPointDiscoverySetup
                {
                    ServiceName = serviceName,
                    ServiceNamespace = serviceNamespace,
                    PortName = portName,
                    RequiredContactPointsNr = requiredContactPoints
                }
            }, autoStart);
        
        /// <summary>
        ///     Adds Akka.Management.Cluster.Bootstrap support to the <see cref="ActorSystem"/>.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies a <see cref="ClusterBootstrapSetup"/> instance, used
        ///     to configure Akka.Management.Cluster.Bootstrap.
        /// </param>
        /// <param name="autoStart">
        ///     When set to true, cluster bootstrap will be started automatically on <see cref="ActorSystem"/> startup.
        ///     Note that this will also automatically start Akka.Management on startup.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example >
        ///     Starting Cluster.Bootstrap manually:
        ///     <code>
        ///         builder.WithClusterBootstrap(setup => {
        ///             setup.ContactPointDiscovery.ServiceName = "myService"
        ///         }, autoStart: false);
        ///         builder.AddStartup(async (system, registry) =>
        ///         {
        ///             await AkkaManagement.Get(system).Start();
        ///             await ClusterBootstrap.Get(system).Start();
        ///         });
        ///     </code>
        /// </example>
        public static AkkaConfigurationBuilder WithClusterBootstrap(
            this AkkaConfigurationBuilder builder,
            Action<ClusterBootstrapSetup> configure,
            bool autoStart = true)
        {
            var setup = new ClusterBootstrapSetup
            {
                ContactPoint = new ContactPointSetup(),
                ContactPointDiscovery = new ContactPointDiscoverySetup(),
                JoinDecider = new JoinDeciderSetup()
            };
            configure(setup);
            return builder.WithClusterBootstrap(setup, autoStart);
        }

        /// <summary>
        ///     Adds Akka.Management.Cluster.Bootstrap support to the <see cref="ActorSystem"/>.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="setup">
        ///     The <see cref="ClusterBootstrapSetup"/> that will be used to configure Akka.Management.Cluster.Bootstrap
        /// </param>
        /// <param name="autoStart">
        ///     When set to true, cluster bootstrap will be started automatically on <see cref="ActorSystem"/> startup.
        ///     Note that this will also automatically start Akka.Management on startup.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example >
        ///     Starting Cluster.Bootstrap manually:
        ///     <code>
        ///         builder.WithClusterBootstrap( new ClusterBootstrapSetup {
        ///             ContactPointDiscovery = new ContactPointDiscoverySetup {
        ///                 ServiceName = "myService"
        ///             }
        ///         }, autoStart: false);
        ///         builder.AddStartup(async (system, registry) =>
        ///         {
        ///             await AkkaManagement.Get(system).Start();
        ///             await ClusterBootstrap.Get(system).Start();
        ///         });
        ///     </code>
        /// </example>
        public static AkkaConfigurationBuilder WithClusterBootstrap(
            this AkkaConfigurationBuilder builder,
            ClusterBootstrapSetup setup,
            bool autoStart = true)
        {
            if (autoStart)
            {
                // Inject ClusterBootstrapProvider into akka.extensions
                builder.WithExtensions(typeof(ClusterBootstrapProvider));
            }
        
            // Cluster bootstrap routes needs to be added for it to work with Akka.Management
            builder.AddHocon(
                $"akka.management.http.routes.cluster-bootstrap = \"{typeof(ClusterBootstrapProvider).AssemblyQualifiedName}\"", 
                HoconAddMode.Prepend);
            
            builder.AddSetup(setup);
            return builder;
        }
    }
}