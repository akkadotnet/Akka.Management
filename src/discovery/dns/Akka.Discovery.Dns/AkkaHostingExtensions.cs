
using System;
using Akka.Actor.Setup;
using Akka.Hosting;

namespace Akka.Discovery.Dns
{
    /// <summary>
    /// Extensions for configuring DNS-based service discovery with the Akka.Hosting API.
    /// </summary>
    public static class AkkaHostingExtensions
    {
        /// <summary>
        /// Adds DNS service discovery to the <see cref="AkkaConfigurationBuilder"/>.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="configure">Action that configures the <see cref="DnsDiscoveryOptions"/>.</param>
        /// <returns>The same builder instance.</returns>
        public static AkkaConfigurationBuilder WithDnsDiscovery(
            this AkkaConfigurationBuilder builder,
            Action<DnsDiscoveryOptions> configure)
        {
            var options = new DnsDiscoveryOptions();
            configure(options);
            options.Apply(builder);
            builder.AddSetup(new DnsDiscoverySetup { DiscoveryId = options.ConfigPath });
            
            return builder;
        }
        
        /// <summary>
        /// Adds DNS service discovery to the <see cref="AkkaConfigurationBuilder"/> with default options.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <returns>The same builder instance.</returns>
        public static AkkaConfigurationBuilder WithDnsDiscovery(
            this AkkaConfigurationBuilder builder)
        {
            return builder.WithDnsDiscovery(_ => { });
        }
        
        /// <summary>
        /// Adds DNS service discovery to the <see cref="AkkaConfigurationBuilder"/> with the specified options.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="options">The options.</param>
        /// <returns>The same builder instance.</returns>
        public static AkkaConfigurationBuilder WithDnsDiscovery(
            this AkkaConfigurationBuilder builder,
            DnsDiscoveryOptions options)
        {
            options.Apply(builder);
            builder.AddSetup(new DnsDiscoverySetup { DiscoveryId = options.ConfigPath });
            
            return builder;
        }
        
        /// <summary>
        /// Sets DNS as the default discovery method for Akka.NET.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="discoveryId">The discovery ID.</param>
        /// <returns>The same builder instance.</returns>
        public static AkkaConfigurationBuilder WithDnsDiscoveryDefault(
            this AkkaConfigurationBuilder builder,
            string discoveryId = DnsDiscoveryOptions.DefaultPath)
        {
            builder.AddHocon("akka.discovery.method = " + discoveryId, HoconAddMode.Prepend);
            return builder;
        }
    }
}
