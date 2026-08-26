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
                            .WithKubernetesDiscovery(new KubernetesDiscoveryOptions
                            {
                                PodNamespace = "underTest",
                            });
                    });
                });

            using var host = hostBuilder.Build();
            await host.StartAsync();
                
            var system = host.Services.GetRequiredService<ActorSystem>();
                
            var settings = KubernetesDiscoverySettings.Create(system);
            Assert.Equal("underTest", settings.PodNamespace);
                
            Assert.Equal("kubernetes-api", system.Settings.Config.GetString("akka.discovery.method"));
            
            var config = system.Settings.Config.GetConfig("akka.discovery.kubernetes-api");
            Assert.NotNull(config);
        }
    }
}