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
            string testName,
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
            var startups = new (string, Action<AkkaConfigurationBuilder>)[]
            {
                ("1. Parameterized method, auto-starting", builder =>
                {
                    builder.WithClusterBootstrap("testService", requiredContactPoints: 1, autoStart: true);
                }),
                ("2. Parameterized method, manual start", builder =>
                {
                    builder.WithClusterBootstrap("testService", requiredContactPoints: 1, autoStart: false);
                    builder.AddStartup(async (system, registry) =>
                    {
                        await AkkaManagement.Get(system).Start();
                        await ClusterBootstrap.Get(system).Start();
                    });
                }),
                
                ("3. Setup delegate method, auto-starting", builder =>
                {
                    builder.WithClusterBootstrap(setup =>
                    {
                        setup.ContactPointDiscovery.ServiceName = "testService";
                        setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
                    }, autoStart: true);
                }),
                ("4. Setup delegate method, manual start", builder =>
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
                }),
                
                // Should start normally when both Akka.Management and ClusterBootstrap is auto-started
                ("5. AkkaManagement in extensions list and auto-start ClusterBootstrap", builder =>
                {
                    builder.WithExtensions(typeof(AkkaManagementProvider));
                    builder.WithClusterBootstrap("testService", requiredContactPoints: 1, autoStart: true);
                }),
                ("6. AkkaManagement and ClusterBootstrap declared first in extensions list", builder =>
                {
                    builder.WithExtensions(
                        typeof(AkkaManagementProvider),
                        typeof(ClusterBootstrapProvider));
                    builder.WithClusterBootstrap("testService", requiredContactPoints: 1, autoStart: false);
                }),
                ("7. AkkaManagement in extensions list, WithClusterBootstrap declared first", builder =>
                {
                    builder.WithClusterBootstrap("testService", requiredContactPoints: 1, autoStart: true);
                    builder.WithExtensions(typeof(AkkaManagementProvider));
                }),
                ("8. AkkaManagement and ClusterBootstrap in extensions list, WithClusterBootstrap declared first", builder =>
                {
                    builder.WithClusterBootstrap("testService", requiredContactPoints: 1, autoStart: false);
                    builder.WithExtensions(
                        typeof(AkkaManagementProvider),
                        typeof(ClusterBootstrapProvider));
                }),
            };
            
            foreach (var startup in startups)
            {
                yield return new object[]
                {
                    startup.Item1, startup.Item2
                };
            }
        }
    }
}