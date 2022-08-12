// -----------------------------------------------------------------------
//  <copyright file="HostingSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Remote.Hosting;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Management.Cluster.Bootstrap.Tests
{
    public class HostingSpecs
    {
        private async Task<IHost> StartHost(
            Action<AkkaConfigurationBuilder> testSetup,
            LogLevel minimumLogLevel = LogLevel.Debug)
        {
            var host = new HostBuilder()
                .ConfigureLogging((_, builder) =>
                {
                    builder.ClearProviders();
                    builder.AddFilter(level => level >= minimumLogLevel);
                    builder.AddProvider(new XUnitLoggerProvider(_output, minimumLogLevel));
                })
                .ConfigureServices(services =>
                {
                    services.AddAkka("TestSystem", builder =>
                    {
                        builder.ConfigureLoggers(logger =>
                        {
                            logger.LogLevel = Event.LogLevel.DebugLevel;
                            logger.AddLoggerFactory();
                        });
                        builder.WithRemoting(hostname: "localhost", port: 12552);
                        builder.WithClustering();
                        builder.WithAkkaManagement("localhost", 18558, "localhost", 18558);
                        builder.WithConfigDiscovery(
                            new Dictionary<string, List<string>>
                            {
                                ["testService"] = new List<string> { "localhost:18558" }
                            });
                        testSetup(builder);
                    });
                }).Build();
        
            await host.StartAsync();
            return host;
        }

        private readonly ITestOutputHelper _output;
        
        public HostingSpecs(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory(DisplayName = "WithClusterBootstrap should work")]
        [MemberData(nameof(StartupFactory))]
        public async Task WithClusterBootstrapTest(
            Action<AkkaConfigurationBuilder> startupAction)
        {
            var tcs = new TaskCompletionSource<Done>();
            using var host = await StartHost(startupAction);

            var system = (ActorSystem) host.Services.GetService(typeof(ActorSystem));
            var cluster = Akka.Cluster.Cluster.Get(system);
            cluster.RegisterOnMemberUp(() =>
            {
                tcs.SetResult(Done.Instance);
            });

            tcs.Task.Wait(30.Seconds()).Should().BeTrue();
        }

        public static IEnumerable<object[]> StartupFactory()
        {
            var startups = new Action<AkkaConfigurationBuilder>[]
            {
                builder =>
                {
                    // need to cheat a little because the default is 2, which would never work
                    builder.AddHocon(
                        "akka.management.cluster.bootstrap.contact-point-discovery.required-contact-point-nr = 1");
                    builder.WithClusterBootstrap("testService", autoStart: true);
                },
                builder =>
                {
                    // need to cheat a little because the default is 2, which would never work
                    builder.AddHocon(
                        "akka.management.cluster.bootstrap.contact-point-discovery.required-contact-point-nr = 1");
                    builder.WithClusterBootstrap("testService", autoStart: false);
                    builder.AddStartup(async (system, registry) =>
                    {
                        await AkkaManagement.Get(system).Start();
                        await ClusterBootstrap.Get(system).Start();
                    });
                },
                
                builder =>
                {
                    builder.WithClusterBootstrap(setup =>
                    {
                        setup.ContactPointDiscovery.ServiceName = "testService";
                        setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
                    }, true);
                },
                builder =>
                {
                    builder.WithClusterBootstrap(setup =>
                    {
                        setup.ContactPointDiscovery.ServiceName = "testService";
                        setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
                    }, false);
                    builder.AddStartup(async (system, registry) =>
                    {
                        await AkkaManagement.Get(system).Start();
                        await ClusterBootstrap.Get(system).Start();
                    });
                },
            };
            
            foreach (var startup in startups)
            {
                yield return new object[]
                {
                    startup
                };
            }
        }
    }
}