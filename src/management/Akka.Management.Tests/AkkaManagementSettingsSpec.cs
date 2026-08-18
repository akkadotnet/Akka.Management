// -----------------------------------------------------------------------
//  <copyright file="AkkaManagementSettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Http.Dsl;
using Akka.Management.Dsl;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Management.Tests
{
    public class AkkaManagementSettingsSpec
    {
        [Fact(DisplayName = "AkkaManagementSettings should contain default values")]
        public void SettingsDefaultValues()
        {
            var settings = AkkaManagementSettings.Create(AkkaManagementProvider.DefaultConfiguration());
            var http = settings.Http;
            
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            var defaultHostname = addresses
                .First(ip => !Equals(ip, IPAddress.Any) && !Equals(ip, IPAddress.IPv6Any))
                .ToString();

            Assert.Equal(defaultHostname, http.Hostname);
            Assert.Equal(8558, http.Port);
            Assert.Equal(defaultHostname, http.EffectiveBindHostname);
            Assert.Equal(8558, http.EffectiveBindPort);
            Assert.Empty(http.BasePath);
            Assert.Equal(3, http.RouteProviders.Count);

            Assert.Equal("cluster-bootstrap", http.RouteProviders[0].Name);
            Assert.Equal("Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management", http.RouteProviders[0].FullyQualifiedClassName);

            Assert.Equal("remote-address", http.RouteProviders[1].Name);
            Assert.Equal("Akka.Management.Routes.AddressRouteProvider, Akka.Management", http.RouteProviders[1].FullyQualifiedClassName);

            Assert.Equal("cluster-client-receptionist", http.RouteProviders[2].Name);
            Assert.Equal("Akka.Management.Routes.ClusterClientReceptionistRouteProvider, Akka.Management", http.RouteProviders[2].FullyQualifiedClassName);

            Assert.True(http.RouteProvidersReadOnly);
        }

        [Fact(DisplayName = "AkkaManagementOptions should contain default values")]
        public void OptionsDefaultValuesTest()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            builder.WithAkkaManagement(new AkkaManagementOptions());
            
            var settings = AkkaManagementSettings.Create(builder.Configuration.Value);
            var http = settings.Http;
            
            var addresses = Dns.GetHostAddresses(Dns.GetHostName());
            var defaultHostname = addresses
                .First(ip => !Equals(ip, IPAddress.Any) && !Equals(ip, IPAddress.IPv6Any))
                .ToString();

            Assert.Equal(defaultHostname, http.Hostname);
            Assert.Equal(8558, http.Port);
            Assert.Equal(defaultHostname, http.EffectiveBindHostname);
            Assert.Equal(8558, http.EffectiveBindPort);
            Assert.Empty(http.BasePath);
            Assert.Equal(3, http.RouteProviders.Count);

            Assert.Equal("cluster-bootstrap", http.RouteProviders[0].Name);
            Assert.Equal("Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management", http.RouteProviders[0].FullyQualifiedClassName);

            Assert.Equal("remote-address", http.RouteProviders[1].Name);
            Assert.Equal("Akka.Management.Routes.AddressRouteProvider, Akka.Management", http.RouteProviders[1].FullyQualifiedClassName);

            Assert.Equal("cluster-client-receptionist", http.RouteProviders[2].Name);
            Assert.Equal("Akka.Management.Routes.ClusterClientReceptionistRouteProvider, Akka.Management", http.RouteProviders[2].FullyQualifiedClassName);

            Assert.True(http.RouteProvidersReadOnly);
        }

        [Fact(DisplayName = "AkkaManagementSetup should override AkkaManagementSettings value")]
        public void SetupOverrideSettings()
        {
            var setup = new AkkaManagementSetup(new HttpSetup
            {
                HostName = "a",
                Port = 1234,
                BindHostName = "b",
                BindPort = 1235,
                BasePath = "c",
                RouteProvidersReadOnly = false,
            });
            setup.Http.WithRouteProvider<FakeRouteProvider>("test");
            var settings = setup.Apply(AkkaManagementSettings.Create(AkkaManagementProvider.DefaultConfiguration()));
            var http = settings.Http;
            
            Assert.Equal("a", http.Hostname);
            Assert.Equal(1234, http.Port);
            Assert.Equal("b", http.EffectiveBindHostname);
            Assert.Equal(1235, http.EffectiveBindPort);
            Assert.Equal("c", http.BasePath);
            Assert.Equal(4, http.RouteProviders.Count);
            // converted from BeEquivalentTo (NamedRouteProvider has value equality over Name + FullyQualifiedClassName)
            Assert.Equal(new NamedRouteProvider("cluster-bootstrap", "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management"), http.RouteProviders[0]);
            Assert.Equal(new NamedRouteProvider("remote-address", "Akka.Management.Routes.AddressRouteProvider, Akka.Management"), http.RouteProviders[1]);
            Assert.Equal(new NamedRouteProvider("cluster-client-receptionist", "Akka.Management.Routes.ClusterClientReceptionistRouteProvider, Akka.Management"), http.RouteProviders[2]);
            Assert.Equal(new NamedRouteProvider("test", typeof(FakeRouteProvider).AssemblyQualifiedName), http.RouteProviders[3]);
            Assert.False(http.RouteProvidersReadOnly);
        }

        [Fact(DisplayName = "AkkaManagementOptions should override default values")]
        public void OptionsOverrideConfigTest()
        {
            var options = new AkkaManagementOptions
            {
                HostName = "a",
                Port = 1234,
                BindHostName = "b",
                BindPort = 1235,
                BasePath = "c",
                RouteProvidersReadOnly = false
            };
            options.WithRouteProvider<FakeRouteProvider>("test");
            
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            builder.WithAkkaManagement(options);
            
            var settings = AkkaManagementSettings.Create(builder.Configuration.Value);
            var http = settings.Http;

            Assert.Equal("a", http.Hostname);
            Assert.Equal(1234, http.Port);
            Assert.Equal("b", http.EffectiveBindHostname);
            Assert.Equal(1235, http.EffectiveBindPort);
            Assert.Equal("c", http.BasePath);
            Assert.Equal(4, http.RouteProviders.Count);
            // converted from BeEquivalentTo (NamedRouteProvider has value equality over Name + FullyQualifiedClassName)
            Assert.Equal(new NamedRouteProvider("cluster-bootstrap", "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management"), http.RouteProviders[0]);
            Assert.Equal(new NamedRouteProvider("remote-address", "Akka.Management.Routes.AddressRouteProvider, Akka.Management"), http.RouteProviders[1]);
            Assert.Equal(new NamedRouteProvider("cluster-client-receptionist", "Akka.Management.Routes.ClusterClientReceptionistRouteProvider, Akka.Management"), http.RouteProviders[2]);
            Assert.Equal(new NamedRouteProvider("test", typeof(FakeRouteProvider).AssemblyQualifiedName), http.RouteProviders[3]);
            Assert.False(http.RouteProvidersReadOnly);
        }

        [Fact(DisplayName = "AkkaManagementSetup.Apply should throw on invalid route provider type")]
        public void InvalidRouteProviderType()
        {
            var setup = new AkkaManagementSetup(new HttpSetup
            {
                RouteProviders =
                {
                    ["test"] = typeof(FakeRouteProvider),
                    ["invalid-route"] = typeof(InvalidRouteProvider)
                }
            });

            // ThrowExactly<ConfigurationException> -> Assert.Throws (exact type); WithMessage globs are case-insensitive (FA MatchEquivalentOf)
            var ex = Assert.Throws<ConfigurationException>(() =>
            {
                setup.Apply(AkkaManagementSettings.Create(AkkaManagementProvider.DefaultConfiguration()));
            });
            Assert.Contains("invalid-route", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("InvalidRouteProvider", ex.Message, StringComparison.OrdinalIgnoreCase);

            var ex2 = Assert.Throws<ConfigurationException>(() =>
            {
                setup.Http.WithRouteProvider<FakeRouteProvider>("test2");
            });
            Assert.EndsWith("already added", ex2.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact(DisplayName = "AkkaManagementOptions.Apply should throw on invalid route provider type")]
        public void OptionsInvalidRouteProviderType()
        {
            var options = new AkkaManagementOptions
            {
                RouteProviders = new Dictionary<string, Type?>
                {
                    ["test"] = typeof(FakeRouteProvider),
                    ["invalid-route"] = typeof(InvalidRouteProvider)
                }
            };
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");

            // ThrowExactly<ConfigurationException> -> Assert.Throws (exact type); WithMessage globs are case-insensitive (FA MatchEquivalentOf)
            var ex = Assert.Throws<ConfigurationException>(() =>
            {
                options.Apply(builder);
            });
            Assert.Contains("invalid-route", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("InvalidRouteProvider", ex.Message, StringComparison.OrdinalIgnoreCase);

            var ex2 = Assert.Throws<ConfigurationException>(() =>
            {
                options.WithRouteProvider<FakeRouteProvider>("test2");
            });
            Assert.EndsWith("already added", ex2.Message, StringComparison.OrdinalIgnoreCase);
        }

        private class InvalidRouteProvider
        {
        }
        
        private class FakeRouteProvider: IManagementRouteProvider
        {
            public Route[] Routes(ManagementRouteProviderSettings settings)
            {
                throw new NotImplementedException();
            }
        }
    }
}