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
using Akka.Discovery.Azure.Tests.Utils;
using Akka.Hosting;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Akka.Discovery.Azure.Tests
{
    [Collection(nameof(AzuriteSpecs))]
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
                        builder.WithAkkaManagement(config =>
                        {
                            config.Http.HostName = "localhost";
                            config.Http.Port = 18558;
                            config.Http.BindHostName = "localhost";
                            config.Http.BindPort = 18558;
                        });
                        builder.WithClusterBootstrap(setup =>
                        {
                            setup.ContactPointDiscovery.ServiceName = "testService";
                            setup.ContactPointDiscovery.RequiredContactPointsNr = 1;
                        });
                        testSetup(builder);
                    });
                }).Build();

            await host.StartAsync();
            return host;
        }

        private readonly ITestOutputHelper _output;
        private readonly AzuriteFixture _azuriteFixture;
        private readonly string _connectionString;
        
        public HostingSpecs(ITestOutputHelper output, AzuriteFixture azuriteFixture)
        {
            _output = output;
            _azuriteFixture = azuriteFixture;
            _connectionString = azuriteFixture.ConnectionString;
        }

        [Theory(DisplayName = "WithAzureDiscovery should work")]
        [MemberData(nameof(Startups))]
        public async Task WithAzureDiscoveryTest(
            Func<string, AkkaConfigurationBuilder, AkkaConfigurationBuilder> startupAction)
        {
            await DbUtils.Cleanup(_connectionString);

            var tcs = new TaskCompletionSource<Done>();
            using var host = await StartHost(builder => startupAction(_connectionString, builder));

            var system = host.Services.GetRequiredService<ActorSystem>();
            var cluster = Cluster.Cluster.Get(system);
            cluster.RegisterOnMemberUp(() => { tcs.SetResult(Done.Instance); });

            await tcs.Task.WaitAsync(30.Seconds());
        }

        public static readonly TheoryData<Func<string, AkkaConfigurationBuilder, AkkaConfigurationBuilder>> Startups =
            new()
            {
                (conn, builder) => builder.WithAzureDiscovery(conn, "testService", "localhost", 18558),
                (conn, builder) => builder.WithAzureDiscovery(setup =>
                {
                    setup.ConnectionString = conn;
                    setup.ServiceName = "testService";
                    setup.HostName = "localhost";
                    setup.Port = 18558;
                }),
                (conn, builder) =>
                {
                    var setup = new AzureDiscoveryOptions
                    {
                        ConnectionString = conn,
                        ServiceName = "testService",
                        HostName = "localhost",
                        Port = 18558
                    };
                    return builder.WithAzureDiscovery(setup);
                }
                // Could not test DefaultAzureCredential because that requires HTTPS and bearer token,
                // and azurite does not support that
            };
    }
}