// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoveryExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Azure;
using Akka.Hosting;

namespace Akka.Management.Hosting
{
    public static class AzureDiscoveryExtensions
    {
        /// <summary>
        ///     Adds Akka.Discovery.Azure support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="connectionString">
        ///     The connection string used to connect to Azure Table hosting the cluster membership table
        /// </param>
        /// <param name="serviceName">
        ///     The service name assigned to the cluster.
        /// </param>
        /// <param name="publicHostname">
        ///     The public IP/host of this node, usually for akka management. It will be used by other nodes to connect
        ///     and query this node. Defaults to <see cref="Dns"/>
        /// </param>
        /// <param name="publicPort">
        ///     The public port of this node, usually for akka management. It will be used by other nodes to connect
        ///     and query this node. Defaults to 8558
        /// </param>
        /// <returns>
        ///     The same <see cref="ActorSystem"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAzureDiscovery(
            this AkkaConfigurationBuilder builder,
            string connectionString,
            string serviceName = null,
            string publicHostname = null,
            int? publicPort = null)
        {
            var setup = new AzureDiscoverySetup
            {
                ConnectionString = connectionString,
                ServiceName = serviceName,
                HostName = publicHostname,
                Port = publicPort
            };
            return builder.WithAzureDiscovery(setup);
        }

        /// <summary>
        ///     Adds Akka.Discovery.Azure support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies an <see cref="AzureDiscoverySetup"/> instance, used
        ///     to configure Akka.Discovery.Azure.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAzureDiscovery(
            this AkkaConfigurationBuilder builder,
            Action<AzureDiscoverySetup> configure)
        {
            var setup = new AzureDiscoverySetup();
            configure(setup);
            return builder.WithAzureDiscovery(setup);
        }

        /// <summary>
        ///     Adds Akka.Discovery.Azure support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="setup">
        ///     The <see cref="AzureDiscoverySetup"/> instance used to configure Akka.Discovery.Azure.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAzureDiscovery(
            this AkkaConfigurationBuilder builder,
            AzureDiscoverySetup setup)
        {
            builder.AddHocon(
                ((Config)"akka.discovery.method = azure").WithFallback(AzureServiceDiscovery.DefaultConfig), 
                HoconAddMode.Prepend);

            builder.AddSetup(setup);
            return builder;
        }
    }
}