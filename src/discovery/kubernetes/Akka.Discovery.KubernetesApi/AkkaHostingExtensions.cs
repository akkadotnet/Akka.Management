// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Hosting;

namespace Akka.Discovery.KubernetesApi
{
    public static class AkkaHostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Discovery.KubernetesApi support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="podLabelSelector">
        ///     <para>
        ///         Optional. Pod label selector value to query pod API with. See the official Kubernetes documentation on 
        ///         <a href="https://kubernetes.io/docs/concepts/overview/working-with-objects/labels/#label-selectors">
        ///         label selectors</a> for more information.
        ///     </para>
        ///     <b>Default</b>: "app={0}", where "{0}" will be replaced by
        ///     <c>ClusterBootstrapSetup.ContactPointDiscovery.ServiceName</c> value.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <example>
        ///   <code>
        ///     // In this code sample, the final label selector would be "app=testService".
        ///     services.AddAkka("mySystem", builder => {
        ///         builder
        ///             .WithClustering()
        ///             .WithClusterBootstrap(options =>
        ///             {
        ///                 options.ContactPointDiscovery.ServiceName = "testService";
        ///             }, autoStart: true)
        ///             .WithKubernetesDiscovery();
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithKubernetesDiscovery(
            this AkkaConfigurationBuilder builder,
            string? podLabelSelector = null)
        {
            return builder.WithKubernetesDiscovery(new KubernetesDiscoveryOptions
            {
                PodLabelSelector = podLabelSelector
            });
        }
        
        /// <summary>
        ///     Adds Akka.Discovery.KubernetesApi support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies an <see cref="KubernetesDiscoveryOptions"/> instance, used
        ///     to configure Akka.Discovery.KubernetesApi.
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
        ///             }, autoStart: true)
        ///             .WithKubernetesDiscovery(options => {
        ///                 options.PodNamespace = "my-cluster-namespace";
        ///             });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithKubernetesDiscovery(
            this AkkaConfigurationBuilder builder,
            Action<KubernetesDiscoveryOptions> configure)
        {
            var options = new KubernetesDiscoveryOptions();
            configure(options);
            return builder.WithKubernetesDiscovery(options);
        }
        
        /// <summary>
        ///     Adds Akka.Discovery.KubernetesApi support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the discovery plugin, you will still need to add ClusterBootstrap for
        ///     a complete solution.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="options">
        ///     The <see cref="KubernetesDiscoveryOptions"/> instance used to configure Akka.Discovery.KubernetesApi.
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
        ///             }, autoStart: true)
        ///             .WithKubernetesDiscovery(new KubernetesDiscoveryOptions {
        ///                 PodNamespace = "my-cluster-namespace"
        ///             });
        ///     }
        ///   </code>
        /// </example>
        public static AkkaConfigurationBuilder WithKubernetesDiscovery(
            this AkkaConfigurationBuilder builder,
            KubernetesDiscoveryOptions options)
        {
            options.Apply(builder);
            builder.AddHocon($"akka.discovery.method = {options.ConfigPath}", HoconAddMode.Prepend);
            builder.AddHocon(KubernetesDiscovery.DefaultConfiguration(), HoconAddMode.Append);
            
            // Force start the module
            builder.AddStartup((system, _) =>
            {
                KubernetesDiscovery.Get(system);
            });
            return builder;
        }
    }
}