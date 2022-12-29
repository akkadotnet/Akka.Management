﻿// -----------------------------------------------------------------------
//  <copyright file="HostingSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Hosting;
using Akka.Http.Dsl;
using Akka.Management.Dsl;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

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
                        })
                            .AddHocon(TestKit.Xunit2.TestKit.DefaultConfig, HoconAddMode.Append);
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
            var sys = host.Services.GetService<ActorSystem>();
            var testKit = new TestKit.Xunit2.TestKit(sys);

            var client = new HttpClient();
            await testKit.AwaitAssertAsync(async () =>
            {
                var response = await client.GetAsync("http://localhost:18558/bootstrap/seed-nodes");
                response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            });
        }

        public static IEnumerable<object[]> StartupFactory()
        {
            var startups = new Action<AkkaConfigurationBuilder>[]
            {
                builder =>
                {
                    builder
                        .WithAkkaManagement("localhost", 18558, "localhost", 18558)
                        
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
                        setup.Http.HostName = "localhost";
                        setup.Http.Port = 18558;
                        setup.Http.BindHostName = "localhost";
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
                        setup.Http.HostName = "localhost";
                        setup.Http.Port = 18558;
                        setup.Http.BindHostName = "localhost";
                        setup.Http.BindPort = 18558;
                    }, true);
                },
                
                builder =>
                {
                    var setup = new AkkaManagementSetup( new HttpSetup
                        {
                            HostName = "localhost",
                            Port = 18558,
                            BindHostName = "localhost",
                            BindPort = 18558,
                        });
                    builder.WithAkkaManagement(setup)
                    .AddStartup(async (system, _) =>
                    {
                        await AkkaManagement.Get(system).Start();
                    });
                },
                builder =>
                {
                    var setup = new AkkaManagementSetup(new HttpSetup
                    {
                        HostName = "localhost",
                        Port = 18558,
                        BindHostName = "localhost",
                        BindPort = 18558,
                    });
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