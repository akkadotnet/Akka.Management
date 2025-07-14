
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
        // /// <summary>
        // /// Adds DNS service discovery to the <see cref="AkkaConfigurationBuilder"/>.
        // /// </summary>
        // /// <param name="builder">The builder instance.</param>
        // /// <param name="configure">Action that configures the <see cref="DnsDiscoveryOptions"/>.</param>
        // /// <returns>The same builder instance.</returns>
        public static AkkaConfigurationBuilder WithDnsDiscovery(
            this AkkaConfigurationBuilder builder,
            Action<DnsDiscoveryOptions>? configure = null,
            string discoveryId = DnsDiscoveryOptions.DefaultPath,
            bool makeDefault = true)
        {
            var options = new DnsDiscoveryOptions();
            configure?.Invoke(options);
            options.Apply(builder);
            builder.AddSetup(new DnsDiscoverySetup { DiscoveryId = options.ConfigPath });
            if (makeDefault)
            {
                builder.SetDiscoveryMethod(discoveryId);

            }
            return builder;
        }
        
        /// <summary>
        /// Sets DNS as the default discovery method for Akka.NET.
        /// </summary>
        /// <param name="builder">The builder instance.</param>
        /// <param name="discoveryId">The discovery ID.</param>
        /// <returns>The same builder instance.</returns>
        public static AkkaConfigurationBuilder SetDiscoveryMethod(
            this AkkaConfigurationBuilder builder,
            string discoveryId)
        {
            builder.AddHocon("akka.discovery.method = " + discoveryId, HoconAddMode.Prepend);
            return builder;
        }
        
        
        public static AkkaConfigurationBuilder WithDnsResolver(
            this AkkaConfigurationBuilder builder,
            string resolverId = "inet-address")
        {
            builder.AddHocon($"akka.io.dns.resolver = {resolverId}", HoconAddMode.Prepend);
            return builder;
        }
        
        public static AkkaConfigurationBuilder WithAsyncDnsResolver(
            this AkkaConfigurationBuilder builder, Action<AsyncDnsResolverOptions>? configure = null)
        {
            builder.WithDnsResolver(AsyncDnsResolverOptions.DefaultPath);
            var opt = new AsyncDnsResolverOptions();
            configure?.Invoke(opt);
            opt.Apply(builder);
            return builder;
        }
    }
}
