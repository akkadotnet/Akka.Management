using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.IO;
using Akka.Management.Dsl;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Management.Tests
{
    internal class HttpManagementEndpointSpecRoutesDotNetDsl : HttpModuleBase, IManagementRouteProvider
    {
        public static bool Started { get; set; }

        public HttpManagementEndpointSpecRoutesDotNetDsl(ActorSystem system)
        {
            Logging.GetLogger(system, this).Info($"{nameof(HttpManagementEndpointSpecRoutesDotNetDsl)} route started");
            Started = true;
        }
        
        public Route[] Routes(ManagementRouteProviderSettings settings)
        {
            return new Route[]{ ("/dotnet", this) };
        }

        public override Task<bool> HandleAsync(IAkkaHttpContext httpContext)
        {
            var context = httpContext.HttpContext;
            if(context.Request.Method != "GET")
                return Task.FromResult(false);
            context.Response.WriteAllAsync("hello .NET Core");
            return Task.FromResult(true);
        }
    }
    
    internal class HttpManagementEndpointSpecRoutesNetFxDsl : HttpModuleBase, IManagementRouteProvider
    {
        public static bool Started { get; set; }

        public HttpManagementEndpointSpecRoutesNetFxDsl(ActorSystem system)
        {
            Logging.GetLogger(system, this).Info($"{nameof(HttpManagementEndpointSpecRoutesNetFxDsl)} route started");
            Started = true;
        }
        
        public Route[] Routes(ManagementRouteProviderSettings settings)
        {
            return new Route[]{ ("/netfx", this) };
        }

        public override Task<bool> HandleAsync(IAkkaHttpContext httpContext)
        {
            var context = httpContext.HttpContext;
            if(context.Request.Method != "GET")
                return Task.FromResult(false);
            context.Response.WriteAllAsync("hello .NET Framework");
            return Task.FromResult(true);
        }
    }
    
    public class AkkaManagementHttpEndpointSpec
    {
        private static Config Config = ConfigurationFactory.ParseString(@"
            akka.remote.log-remote-lifecycle-events = off
            akka.remote.netty.tcp.port = 0
            akka.remote.artery.canonical.port = 0
            #akka.loglevel = DEBUG");

        private readonly ITestOutputHelper _output;
        
        public AkkaManagementHttpEndpointSpec(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(DisplayName = "Management should skip route providers that are set to null")]
        public async Task NulledRouteProviderTest()
        {
            var httpPort = SocketUtil.TemporaryTcpAddress("127.0.0.1").Port;
            var config = ConfigurationFactory.ParseString($@"
                akka.management.http.hostname = ""127.0.0.1""
                akka.management.http.port = {httpPort}
                akka.management.http.routes {{
                    test1 = ""Akka.Management.Tests.HttpManagementEndpointSpecRoutesDotNetDsl, Akka.Management.Tests""
                    test2 = ""Akka.Management.Tests.HttpManagementEndpointSpecRoutesNetFxDsl, Akka.Management.Tests""
                }}");

            var setup = BootstrapSetup.Create()
                .WithConfig(Config.WithFallback(config))
                .And(new AkkaManagementSetup(new HttpSetup
                {
                    RouteProviders = { ["test1"] = null! }
                }));

            HttpManagementEndpointSpecRoutesDotNetDsl.Started = false;
            HttpManagementEndpointSpecRoutesNetFxDsl.Started = false;
            
            var system = ActorSystem.Create("test", setup);
            var extSystem = (ExtendedActorSystem)system;
            var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Tell(new InitializeLogger(system.EventStream));
            
            var management = AkkaManagement.Get(system);
            management.Settings.Http.RouteProviders.Should().Contain(new NamedRouteProvider("test1", null));
            management.Settings.Http.RouteProviders.Should().Contain(new NamedRouteProvider("test2",
                "Akka.Management.Tests.HttpManagementEndpointSpecRoutesNetFxDsl, Akka.Management.Tests"));
            
            await management.Start();
            HttpManagementEndpointSpecRoutesDotNetDsl.Started.Should().BeFalse();
            HttpManagementEndpointSpecRoutesNetFxDsl.Started.Should().BeTrue();
        }
        
        [Fact]
        public async Task ClusterManagementShouldStartAndStopWhenNotSettingAnySecurity()
        {
            var httpPort = SocketUtil.TemporaryTcpAddress("127.0.0.1").Port;
            var configClusterHttpManager = ConfigurationFactory.ParseString($@"
                akka.management.http.hostname = ""127.0.0.1""
                akka.management.http.port = {httpPort}
                akka.management.http.routes {{
                    test1 = ""Akka.Management.Tests.HttpManagementEndpointSpecRoutesDotNetDsl, Akka.Management.Tests""
                    test2 = ""Akka.Management.Tests.HttpManagementEndpointSpecRoutesNetFxDsl, Akka.Management.Tests""
                }}");

            var system = ActorSystem.Create("test", Config.WithFallback(configClusterHttpManager));
            var extSystem = (ExtendedActorSystem)system;
            var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Tell(new InitializeLogger(system.EventStream));

            var management = AkkaManagement.Get(system);
            management.Settings.Http.RouteProviders.Should().Contain(new NamedRouteProvider("test1",
                "Akka.Management.Tests.HttpManagementEndpointSpecRoutesDotNetDsl, Akka.Management.Tests"));
            management.Settings.Http.RouteProviders.Should().Contain(new NamedRouteProvider("test2",
                "Akka.Management.Tests.HttpManagementEndpointSpecRoutesNetFxDsl, Akka.Management.Tests"));

            // Start() should be idempotent, it should return the same Task on multiple invocation
            var tasks = new List<Task<Uri>>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                tasks.Add(management.Start());
                tasks.Add(management.Start());
                tasks.Add(management.Start());

                tasks[1].Should().Be(tasks[0]);
                tasks[2].Should().Be(tasks[0]);
                
                await Task.WhenAll(tasks).WithCancellation(cts.Token);
                
                tasks[1].Result.Should().Be(tasks[0].Result);
                tasks[2].Result.Should().Be(tasks[0].Result);

                var task = management.Start();
                task.Should().Be(tasks[0]);
                await task.WithCancellation(cts.Token);
                task.Result.Should().Be(tasks[0].Result);
            }

            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            var response = await client.GetAsync($"http://127.0.0.1:{httpPort}/dotnet");

            _output.WriteLine(await response.Content.ReadAsStringAsync());
            
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be("hello .NET Core");
            
            response = await client.GetAsync($"http://127.0.0.1:{httpPort}/netfx");
            _output.WriteLine(await response.Content.ReadAsStringAsync());
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be("hello .NET Framework");

            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await management.Stop().WithCancellation(cts.Token);
                }
            }
            finally
            {
                await system.Terminate();
            }
        }
    }

    internal static class TaskExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (token.Register(() => tcs.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new TaskCanceledException(task);
                }
            }

            return await task;
        }
    }
}