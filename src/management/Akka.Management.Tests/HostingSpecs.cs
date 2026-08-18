// -----------------------------------------------------------------------
//  <copyright file="HostingSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Hosting;
using Akka.Http.Dsl;
using Akka.Management.Dsl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Management.Tests
{
    public class HostingSpecs
    {
        // Port allocation for tests - uses a high range unlikely to conflict
        private static int _nextPort = 45000;
        private static readonly object PortLock = new object();
        
        private static int GetTestPort()
        {
            lock (PortLock)
            {
                // Find next available port in range 45000-55000
                const int maxPort = 55000;
                var startPort = _nextPort;
                
                while (_nextPort < maxPort)
                {
                    var currentPort = _nextPort++;
                    if (IsPortAvailable(currentPort))
                        return currentPort;
                }
                
                // Wrap around if we hit the max
                _nextPort = 45000;
                while (_nextPort < startPort)
                {
                    var currentPort = _nextPort++;
                    if (IsPortAvailable(currentPort))
                        return currentPort;
                }
                
                throw new InvalidOperationException($"No available ports found in range 45000-{maxPort}");
            }
        }
        
        private static bool IsPortAvailable(int port)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return true;
            }
            catch
            {
                return false;
            }
        }
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
                            .AddHocon(TestKit.Xunit.TestKit.DefaultConfig, HoconAddMode.Append);
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
            // Use our sequential port allocation to minimize conflicts
            var port = GetTestPort();
            
            using var host = await StartHost(builder => startupAction(builder, port));
            var sys = host.Services.GetService<ActorSystem>();
            var testKit = new TestKit.Xunit.TestKit(sys);

            var client = new HttpClient();
            await testKit.AwaitAssertAsync(async () =>
            {
                var response = await client.GetAsync($"http://localhost:{port}/bootstrap/seed-nodes");
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            }, TimeSpan.FromSeconds(5));
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