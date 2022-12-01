// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Hosting;

namespace Akka.Coordination.Azure
{
    public static class AkkaHostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Coordination.Azure <see cref="Lease"/> support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the lease plugin, you will still need to add the services that depends on
        ///     <see cref="Lease"/> to use this.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="connectionString">
        ///     The Azure Blob Storage connection string
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAzureLease(this AkkaConfigurationBuilder builder, string connectionString)
            => WithAzureLease(builder, new AzureLeaseSetup{ ConnectionString = connectionString });
        
        /// <summary>
        ///     Adds Akka.Coordination.Azure <see cref="Lease"/> support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the lease plugin, you will still need to add the services that depends on
        ///     <see cref="Lease"/> to use this.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="configure">
        ///     An action that modifies an <see cref="AzureLeaseSetup"/> instance, used
        ///     to configure Akka.Coordination.Azure.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAzureLease(
            this AkkaConfigurationBuilder builder,
            Action<AzureLeaseSetup> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            
            var setup = new AzureLeaseSetup();
            configure(setup);
            return WithAzureLease(builder, setup);
        }
        
        /// <summary>
        ///     Adds Akka.Coordination.Azure <see cref="Lease"/> support to the <see cref="ActorSystem"/>.
        ///     Note that this only adds the lease plugin, you will still need to add the services that depends on
        ///     <see cref="Lease"/> to use this.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="setup">
        ///     The <see cref="AzureLeaseSetup"/> instance used to configure Akka.Discovery.Azure.
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        public static AkkaConfigurationBuilder WithAzureLease(
            this AkkaConfigurationBuilder builder,
            AzureLeaseSetup setup)
        {
            builder.AddHocon(AzureLease.DefaultConfiguration, HoconAddMode.Append);
            if (setup != null)
                builder.AddSetup(setup);
            
            return builder;
        }
        
    }
}