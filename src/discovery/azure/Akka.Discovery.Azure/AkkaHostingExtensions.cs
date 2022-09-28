// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoveryExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;
using Akka.Hosting;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Core;

namespace Akka.Discovery.Azure
{
    public static class AkkaHostingExtensions
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
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(setup =>
        ///         {
        ///             setup.ContactPointDiscovery.ServiceName = "testService";
        ///             setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithAzureDiscovery("UseDevelopmentStorage=true");
        ///     }
        ///   </code>
        /// </example>
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
        /// <param name="azureTableEndpoint">
        ///     The <see cref="Uri"/> to the azure table endpoint, this is usually in the form of "https://{yourAccountName}.table.core.windows.net/"
        /// </param>
        /// <param name="azureCredential">
        ///     The <see cref="TokenCredential"/> instance that will be used to authorize the table client with Azure Table Storage service.
        /// </param>
        /// <param name="tableClientOptions">
        ///     Optional <see cref="TableClientOptions"/> instance to configure requests to the Azure Table Storage service.
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
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(setup =>
        ///         {
        ///             setup.ContactPointDiscovery.ServiceName = "testService";
        ///             setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithAzureDiscovery(
        ///             azureTableEndpoint: new Uri("https://{yourAccountName}.table.core.windows.net/"),
        ///             azureCredential: new DefaultAzureCredential()
        ///         );
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAzureDiscovery(
            this AkkaConfigurationBuilder builder,
            Uri azureTableEndpoint,
            TokenCredential azureCredential,
            TableClientOptions tableClientOptions = null,
            string serviceName = null,
            string publicHostname = null,
            int? publicPort = null)
        {
            if (azureCredential == null) throw new ArgumentNullException(nameof(azureCredential));
            var setup = new AzureDiscoverySetup
            {
                AzureTableEndpoint = azureTableEndpoint,
                AzureCredential = azureCredential,
                TableClientOptions = tableClientOptions,
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
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(setup =>
        ///         {
        ///             setup.ContactPointDiscovery.ServiceName = "testService";
        ///             setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithAzureDiscovery( setup => {
        ///             setup.ConnectionString = "UseDevelopmentStorage=true"
        ///         });
        ///     }
        ///   </code>
        /// </example>
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
        /// <example>
        ///   <code>
        ///     services.AddAkka("mySystem", builder => {
        ///         builder.WithClusterBootstrap(setup =>
        ///         {
        ///             setup.ContactPointDiscovery.ServiceName = "testService";
        ///             setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
        ///         }, autoStart: true)
        ///         builder.WithAzureDiscovery( new AzureDiscoverySetup {
        ///             ConnectionString = "UseDevelopmentStorage=true"
        ///         });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithAzureDiscovery(
            this AkkaConfigurationBuilder builder,
            AzureDiscoverySetup setup)
        {
            builder.AddHocon(
                ((Configuration.Config)"akka.discovery.method = azure").WithFallback(AzureServiceDiscovery.DefaultConfig), 
                HoconAddMode.Prepend);

            builder.AddSetup(setup);
            return builder;
        }
    }
}