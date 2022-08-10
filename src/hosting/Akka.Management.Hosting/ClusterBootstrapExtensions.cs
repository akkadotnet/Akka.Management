// -----------------------------------------------------------------------
//  <copyright file="ClusterBootstrapExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Management.Cluster.Bootstrap;

namespace Akka.Management.Hosting
{
    public static class ClusterBootstrapExtensions
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
        /// <param name="newClusterEnabled">
        ///     Cluster Bootstrap will always attempt to join an existing cluster if possible. However
        ///     if no contact point advertises any seed-nodes a new cluster will be formed by the
        ///     node with the lowest address as decided by the <see cref="LowestAddressJoinDecider"/>.
        ///     Setting NewClusterEnabled to false after an initial cluster has formed is recommended to prevent new
        ///     clusters forming during a network partition when nodes are redeployed or restarted.
        /// </param>
        /// <param name="autoStart">
        ///     When set to true, cluster bootstrap will be started automatically on <see cref="ActorSystem"/> startup.
        ///     Note that this will also automatically start Akka.Management on startup.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithClusterBootstrap(
            this AkkaConfigurationBuilder builder,
            string serviceName = null,
            string serviceNamespace = null,
            string portName = null,
            bool? newClusterEnabled = null,
            bool autoStart = true)
            => builder.WithClusterBootstrap(new ClusterBootstrapSetup
            {
                NewClusterEnabled = newClusterEnabled,
                ContactPointDiscovery = new ContactPointDiscoverySetup
                {
                    ServiceName = serviceName,
                    ServiceNamespace = serviceNamespace,
                    PortName = portName
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
        public static AkkaConfigurationBuilder WithClusterBootstrap(
            this AkkaConfigurationBuilder builder,
            Action<ClusterBootstrapSetup> configure,
            bool autoStart = true)
        {
            var setup = new ClusterBootstrapSetup();
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
        public static AkkaConfigurationBuilder WithClusterBootstrap(
            this AkkaConfigurationBuilder builder,
            ClusterBootstrapSetup setup,
            bool autoStart = true)
        {
            if (autoStart)
            {
                // Inject ClusterBootstrapProvider into akka.extensions
                if (builder.Configuration.IsEmpty)
                {
                    var config = (Config)$"akka.extensions=[\"{typeof(ClusterBootstrapProvider).AssemblyQualifiedName}\"]";
                    builder.AddHocon(config, HoconAddMode.Prepend);
                }
                else
                {
                    var extensions = builder.Configuration.Value.GetStringList("akka.extensions").ToList();
                    if (extensions.All(s => !s.Contains(nameof(ClusterBootstrapProvider))))
                    {
                        extensions.Add(typeof(ClusterBootstrapProvider).AssemblyQualifiedName);
                        var config = (Config)$"akka.extensions=[{string.Join(",", extensions.Select(s => $"\"{s}\""))}]";
                        builder.AddHocon(config, HoconAddMode.Prepend);
                    }
                }
            }
        
            builder.AddSetup(setup);
            return builder;
        }
    }
}