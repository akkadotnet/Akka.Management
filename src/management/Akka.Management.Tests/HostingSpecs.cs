// -----------------------------------------------------------------------
//  <copyright file="HostingSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Akka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Management.Tests
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

        [Theory(DisplayName = "WithAkkaManagement should work")]
        [MemberData(nameof(StartupFactory))]
        public async Task WithAkkaManagementTest(
            Action<AkkaConfigurationBuilder> startupAction)
        {
            using var host = await StartHost(startupAction);

            var client = new HttpClient();
            var response = await client.GetAsync("http://localhost:18558/alive");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response = await client.GetAsync("http://localhost:18558/ready");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        public static IEnumerable<object[]> StartupFactory()
        {
            var startups = new Action<AkkaConfigurationBuilder>[]
            {
                builder =>
                {
                    builder.WithAkkaManagement("localhost", 18558, "localhost", 18558)
                    .AddStartup(async (system, _) =>
                    {
                        await AkkaManagement.Get(system).Start();
                    });
                },
                builder =>
                {
                    builder.WithAkkaManagement("localhost", 18558, "localhost", 18558, true);
                },
                
                builder =>
                {
                    builder.WithAkkaManagement(setup =>
                    {
                        setup.Http.Hostname = "localhost";
                        setup.Http.Port = 18558;
                        setup.Http.BindHostname = "localhost";
                        setup.Http.BindPort = 18558;
                    })
                    .AddStartup(async (system, _) =>
                    {
                        await AkkaManagement.Get(system).Start();
                    });
                },
                builder =>
                {
                    builder.WithAkkaManagement(setup =>
                    {
                        setup.Http.Hostname = "localhost";
                        setup.Http.Port = 18558;
                        setup.Http.BindHostname = "localhost";
                        setup.Http.BindPort = 18558;
                    }, true);
                },
                
                builder =>
                {
                    var setup = new AkkaManagementSetup
                    {
                        Http = new HttpSetup
                        {
                            Hostname = "localhost",
                            Port = 18558,
                            BindHostname = "localhost",
                            BindPort = 18558,
                        }
                    };
                    builder.WithAkkaManagement(setup)
                    .AddStartup(async (system, _) =>
                    {
                        await AkkaManagement.Get(system).Start();
                    });
                },
                builder =>
                {
                    var setup = new AkkaManagementSetup
                    {
                        Http = new HttpSetup
                        {
                            Hostname = "localhost",
                            Port = 18558,
                            BindHostname = "localhost",
                            BindPort = 18558,
                        }
                    };
                    builder.WithAkkaManagement(setup, true);
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