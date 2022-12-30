// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Akka.Discovery.KubernetesApi.Tests
{
    public class AkkaHostingSpec
    {
        [Fact(DisplayName = "AkkaConfigurationBuilder extension should inject proper HOCON and Setup")]
        public async Task AkkaConfigurationBuilderTest()
        {
            var hostBuilder = new HostBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddAkka("test", (builder, _) =>
                    {
                        builder
                            .WithKubernetesDiscovery(new KubernetesDiscoverySetup
                            {
                                PodNamespace = "underTest",
                            });
                        
                        var setup = ExtractSetup<KubernetesDiscoverySetup>(builder);
                        setup.Should().NotBeNull();
                        setup!.PodNamespace.Should().Be("underTest");
                        builder.Configuration.HasValue.Should().BeTrue();
                        builder.Configuration.Value.GetString("akka.discovery.method").Should().Be("kubernetes-api");
                    });
                });

            using var host = hostBuilder.Build();
            await host.StartAsync();
                
            var system = host.Services.GetRequiredService<ActorSystem>();
                
            var settings = KubernetesDiscovery.Get(system).Settings;
            settings.PodNamespace.Should().Be("underTest");
                
            system.Settings.Config.GetString("akka.discovery.method").Should().Be("kubernetes-api");
        }
        
        private static T? ExtractSetup<T>(AkkaConfigurationBuilder builder) where T : Setup
            => builder.Setups.FirstOrDefault(s => s is T) as T;
        
    }
}