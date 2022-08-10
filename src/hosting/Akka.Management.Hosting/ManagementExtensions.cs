// -----------------------------------------------------------------------
//  <copyright file="ManagementExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Akka.Actor;
using Akka.Hosting;

namespace Akka.Management.Hosting
{
    public static class AkkaManagementExtensions
    {
        /// <summary>
        ///     Adds Akka.Management support to the <see cref="ActorSystem"/>
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="hostName">
        ///     The hostname where the HTTP Server for Http Cluster Management will be started.
        ///     This defines the interface to use.
        ///     akka.remote.dot-netty.tcp.public-hostname is used if not overriden or empty.
        ///     if akka.remote.dot-netty.tcp.public-hostname is empty, <see cref="Dns.GetHostName"/> is used.
        /// </param>
        /// <param name="port">
        ///     The port where the HTTP Server for Http Cluster Management will be bound.
        ///     The value will need to be from 0 to 65535.
        /// </param>
        /// <param name="bindHostname">
        ///     Use this setting to bind a network interface to a different hostname or ip
        ///     than the HTTP Server for Http Cluster Management.
        ///     Use "0.0.0.0" to bind to all interfaces.
        /// </param>
        /// <param name="bindPort">
        ///     Use this setting to bind a network interface to a different port
        ///     than the HTTP Server for Http Cluster Management. This may be used
        ///     when running akka nodes in a separated networks (under NATs or docker containers).
        ///     Use 0 if you want a random available port.
        /// </param>
        /// <param name="autoStart">
        ///     When set to true, Akka.Management will be started automatically on <see cref="ActorSystem"/> startup.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAkkaManagement(
            this AkkaConfigurationBuilder builder,
            string hostName = null,
            int? port = null,
            string bindHostname = null,
            int? bindPort = null,
            bool autoStart = false)
            => builder.WithAkkaManagement(new AkkaManagementSetup
            {
                Http = new HttpSetup
                {
                    Hostname = hostName,
                    Port = port,
                    BindHostname = bindHostname,
                    BindPort = bindPort
                }
            }, autoStart);

        /// <summary>
        ///     Adds Akka.Management support to the <see cref="ActorSystem"/>
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies an <see cref="AkkaManagementSetup"/> instance, used
        ///     to configure Akka.Management.
        /// </param>
        /// <param name="autoStart">
        ///     When set to true, Akka.Management will be started automatically on <see cref="ActorSystem"/> startup.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAkkaManagement(
            this AkkaConfigurationBuilder builder,
            Action<AkkaManagementSetup> configure,
            bool autoStart = false)
        {
            var setup = new AkkaManagementSetup
            {
                Http = new HttpSetup()
            };
            configure(setup);
            return builder.WithAkkaManagement(setup, autoStart);
        }

        /// <summary>
        ///     Adds Akka.Management support to the <see cref="ActorSystem"/>
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="setup">
        ///     The <see cref="AkkaManagementSetup"/> that will be used to configure Akka.Management
        /// </param>
        /// <param name="autoStart">
        ///     When set to true, Akka.Management will be started automatically on <see cref="ActorSystem"/> startup.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAkkaManagement(
            this AkkaConfigurationBuilder builder,
            AkkaManagementSetup setup,
            bool autoStart = false)
        {
            builder.AddSetup(setup);
            if (autoStart)
            {
                builder.StartActors(async (system, _) =>
                {
                    await AkkaManagement.Get(system).Start();
                });
            }

            return builder;
        }
    }
}