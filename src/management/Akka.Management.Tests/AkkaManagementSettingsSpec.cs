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
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static FluentAssertions.FluentActions;
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

            http.Hostname.Should().Be(defaultHostname);
            http.Port.Should().Be(8558);
            http.EffectiveBindHostname.Should().Be(defaultHostname);
            http.EffectiveBindPort.Should().Be(8558);
            http.BasePath.Should().BeEmpty();
            http.RouteProviders.Count.Should().Be(1);
            http.RouteProviders[0].Name.Should().Be("cluster-bootstrap");
            http.RouteProviders[0].FullyQualifiedClassName.Should().Be("Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management");
            http.RouteProvidersReadOnly.Should().BeTrue();
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

            http.Hostname.Should().Be(defaultHostname);
            http.Port.Should().Be(8558);
            http.EffectiveBindHostname.Should().Be(defaultHostname);
            http.EffectiveBindPort.Should().Be(8558);
            http.BasePath.Should().BeEmpty();
            http.RouteProviders.Count.Should().Be(1);
            http.RouteProviders[0].Name.Should().Be("cluster-bootstrap");
            http.RouteProviders[0].FullyQualifiedClassName.Should().Be("Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management");
            http.RouteProvidersReadOnly.Should().BeTrue();
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
            
            http.Hostname.Should().Be("a");
            http.Port.Should().Be(1234);
            http.EffectiveBindHostname.Should().Be("b");
            http.EffectiveBindPort.Should().Be(1235);
            http.BasePath.Should().Be("c");
            http.RouteProviders.Count.Should().Be(2);
            http.RouteProviders[0].Should()
                .BeEquivalentTo(new NamedRouteProvider("cluster-bootstrap", "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management"));
            http.RouteProviders[1].Should()
                .BeEquivalentTo(new NamedRouteProvider("test", typeof(FakeRouteProvider).AssemblyQualifiedName));
            http.RouteProvidersReadOnly.Should().BeFalse();
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

            http.Hostname.Should().Be("a");
            http.Port.Should().Be(1234);
            http.EffectiveBindHostname.Should().Be("b");
            http.EffectiveBindPort.Should().Be(1235);
            http.BasePath.Should().Be("c");
            http.RouteProviders.Count.Should().Be(2);
            http.RouteProviders[0].Should()
                .BeEquivalentTo(new NamedRouteProvider("cluster-bootstrap", "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management"));
            http.RouteProviders[1].Should()
                .BeEquivalentTo(new NamedRouteProvider("test", typeof(FakeRouteProvider).AssemblyQualifiedName));
            http.RouteProvidersReadOnly.Should().BeFalse();
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

            Invoking(() => setup.Apply(AkkaManagementSettings.Create(AkkaManagementProvider.DefaultConfiguration())))
                .Should().ThrowExactly<ConfigurationException>()
                .WithMessage("*invalid-route*").WithMessage("*InvalidRouteProvider*");

            Invoking(() => setup.Http.WithRouteProvider<FakeRouteProvider>("test2"))
                .Should().ThrowExactly<ConfigurationException>()
                .WithMessage("*already added");
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

            Invoking(() => options.Apply(builder))
                .Should().ThrowExactly<ConfigurationException>()
                .WithMessage("*invalid-route*").WithMessage("*InvalidRouteProvider*");

            Invoking(() => options.WithRouteProvider<FakeRouteProvider>("test2"))
                .Should().ThrowExactly<ConfigurationException>()
                .WithMessage("*already added");
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