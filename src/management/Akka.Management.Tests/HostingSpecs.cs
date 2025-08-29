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
            Action<AkkaConfigurationBuilder, int> startupAction)
        {
            // Try up to 3 times to find a free port and start the service
            Exception? lastException = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var port = SocketUtil.TemporaryTcpAddress("127.0.0.1").Port;
                
                try
                {
                    using var host = await StartHost(builder => startupAction(builder, port));
                    var sys = host.Services.GetService<ActorSystem>();
                    var testKit = new TestKit.Xunit2.TestKit(sys);

                    var client = new HttpClient();
                    await testKit.AwaitAssertAsync(async () =>
                    {
                        var response = await client.GetAsync($"http://localhost:{port}/bootstrap/seed-nodes");
                        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
                    }, TimeSpan.FromSeconds(5));
                    
                    return; // Success!
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to start Akka Management HTTP endpoint"))
                {
                    // Port was likely taken between when we found it free and when we tried to bind
                    lastException = ex;
                    _output.WriteLine($"Attempt {attempt + 1} failed with port {port}, retrying...");
                    await Task.Delay(100); // Small delay before retry
                }
            }
            
            throw new Exception($"Failed to start Akka Management after 3 attempts", lastException);
        }

        public static IEnumerable<object[]> StartupFactory()
        {
            var startups = new Action<AkkaConfigurationBuilder, int>[]
            {
                (builder, port) =>
                {
                    builder
                        .WithAkkaManagement("localhost", port, "localhost", port)
                        .AddStartup(async (system, _) =>
                        {
                            await AkkaManagement.Get(system).Start();
                        });
                },
                (builder, port) =>
                {
                    builder.WithAkkaManagement("localhost", port, "localhost", port, true);
                },
                
                (builder, port) =>
                {
                    builder.WithAkkaManagement(setup =>
                    {
                        setup.Http.HostName = "localhost";
                        setup.Http.Port = port;
                        setup.Http.BindHostName = "localhost";
                        setup.Http.BindPort = port;
                    })
                    .AddStartup(async (system, _) =>
                    {
                        await AkkaManagement.Get(system).Start();
                    });
                },
                (builder, port) =>
                {
                    builder.WithAkkaManagement(setup =>
                    {
                        setup.Http.HostName = "localhost";
                        setup.Http.Port = port;
                        setup.Http.BindHostName = "localhost";
                        setup.Http.BindPort = port;
                    }, true);
                },
                
                (builder, port) =>
                {
                    var setup = new AkkaManagementSetup( new HttpSetup
                        {
                            HostName = "localhost",
                            Port = port,
                            BindHostName = "localhost",
                            BindPort = port,
                        });
                    builder.WithAkkaManagement(setup)
                    .AddStartup(async (system, _) =>
                    {
                        await AkkaManagement.Get(system).Start();
                    });
                },
                (builder, port) =>
                {
                    var setup = new AkkaManagementSetup(new HttpSetup
                    {
                        HostName = "localhost",
                        Port = port,
                        BindHostName = "localhost",
                        BindPort = port,
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