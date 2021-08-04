using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.IO;
using Akka.TestKit.Xunit2.Internals;
using Akka.Util;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Xunit.Abstractions;
using HttpResponse = Akka.Http.Dsl.Model.HttpResponse;

namespace Akka.Management.Tests
{
    internal class HttpManagementEndpointSpecRoutesDotNetDsl : IManagementRouteProvider
    {
        public Route[] Routes(ManagementRouteProviderSettings settings)
        {
            return new Route[]{async ctx =>
            {
                if (ctx.Request.Method != HttpMethods.Get || ctx.Request.Path != "/dotnet")
                    return null;
                return new RouteResult.Complete(
                    HttpResponse.Create(entity: new ResponseEntity(ContentTypes.TextPlainUtf8,
                        ByteString.FromString("hello .NET Core"))));
            }} ;
        }
    }
    
    internal class HttpManagementEndpointSpecRoutesNetFxDsl : IManagementRouteProvider
    {
        public Route[] Routes(ManagementRouteProviderSettings settings)
        {
            return new Route[]{async ctx =>
            {
                if (ctx.Request.Method != HttpMethods.Get || ctx.Request.Path != "/netfx")
                    return null;
                return new RouteResult.Complete(
                    HttpResponse.Create(entity: new ResponseEntity(ContentTypes.TextPlainUtf8,
                        ByteString.FromString("hello .NET Framework"))));
            }};
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

        [Fact]
        public async Task ClusterManagementShouldStartAndStopWhenNotSettingAnySecurity()
        {
            var httpPort = ThreadLocalRandom.Current.Next(10000, 15000);
            var configClusterHttpManager = ConfigurationFactory.ParseString($@"
                //#management-host-port
                akka.management.http.hostname = ""127.0.0.1""
                akka.management.http.port = 8558
                //#management-host-port
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

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                await management.Start().WithCancellation(cts.Token);
            }

            var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            var response = await client.GetAsync($"http://127.0.0.1:{httpPort}/dotnet");
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
        public static async Task WithCancellation(this Task task, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            await using (token.Register(() => tcs.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new TaskCanceledException(task);
                }
            }

            await task;
        }
    }
}