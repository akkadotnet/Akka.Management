// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Sdk;

namespace Akka.Discovery.KubernetesApi.Tests
{
    public class AkkaHostingSpec
    {
        [Fact(DisplayName = "AkkaConfigurationBuilder extension should inject proper HOCON and Setup")]
        public async Task AkkaConfigurationBuilderTest()
        {
            var hostBuilder = new HostBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddAkka("test", (builder, provider) =>
                    {
                        builder
                            .WithKubernetesDiscovery(new KubernetesDiscoverySetup
                            {
                                PodNamespace = "underTest",
                            });
                        
                        var setup = ExtractSetup<KubernetesDiscoverySetup>(builder);
                        setup.Should().NotBeNull();
                        setup.PodNamespace.Should().Be("underTest");
                        builder.Configuration.HasValue.Should().BeTrue();
                        builder.Configuration.Value.GetString("akka.discovery.method").Should().Be("kubernetes-api");
                    });
                });
            
            using (var host = hostBuilder.Build())
            {
                await host.StartAsync();
                
                var system = host.Services.GetRequiredService<ActorSystem>();
                
                var settings = KubernetesDiscovery.Get(system).Settings;
                settings.PodNamespace.Should().Be("underTest");
                
                system.Settings.Config.GetString("akka.discovery.method").Should().Be("kubernetes-api");
            }
        }
        
        private static T ExtractSetup<T>(AkkaConfigurationBuilder builder) where T : Setup
        {
            var type = builder.GetType();
            var fieldInfo = type.GetField("Setups", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
                throw new XunitException("Could not found 'AkkaConfigurationBuilder.Setups' field, AkkaConfigurationBuilder internal API has changed");
            
            var setups = (HashSet<Setup>) fieldInfo.GetValue(builder);
            setups.Should().NotBeNull("'AkkaConfigurationBuilder.Setups' should exist and contains a value");
            return (T) setups!.FirstOrDefault(s => s is KubernetesDiscoverySetup);
        }
        
    }
}