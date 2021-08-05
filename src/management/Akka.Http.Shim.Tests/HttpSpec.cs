using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Server;
using Akka.Http.Dsl.Settings;
using Akka.Http.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Xunit.Abstractions;
using HttpResponse = Akka.Http.Dsl.Model.HttpResponse;

namespace Akka.Http.Shim.Tests
{
    public class HttpSpec : TestKit.Xunit2.TestKit
    {
        public HttpSpec(ITestOutputHelper output) : base(Config.Empty, nameof(HttpSpec), output)
        { }
        
        [Fact]
        public async Task Should_Bind_Properly()
        {
            var baseBuilder = Sys.Http()
                .NewServerAt("localhost", 8081)
                .WithSettings(ServerSettings.Create((ExtendedActorSystem)Sys));

            var serverBinding = await baseBuilder.Bind(new []{RouteOne(), RouteTwo()}.Concat()).ConfigureAwait(false);

            var boundHost = ((DnsEndPoint) serverBinding.LocalAddress).Host;
            var boundPort = ((DnsEndPoint)serverBinding.LocalAddress).Port;
            Log.Info($"Bound Akka Management (HTTP) endpoint to: {boundHost}:{boundPort}");

            await AssertServerIsRunning();
            
            await serverBinding.Terminate(TimeSpan.FromSeconds(5));
        }

        private async Task AssertServerIsRunning()
        {
            using(var client = new HttpClient())
            {
                await TestUrl(client, "http://localhost:8081/test/one", HttpStatusCode.OK);
                await TestUrl(client, "http://localhost:8081/test/two", HttpStatusCode.OK);
                await TestUrl(client, "http://localhost:8081/test", HttpStatusCode.NotFound);
                await TestUrl(client, "http://localhost:8081", HttpStatusCode.NotFound);
                await TestUrl(client, "http://localhost:8081/index.htm", HttpStatusCode.NotFound);
            }
        }

        private async Task TestUrl(HttpClient client, string url, HttpStatusCode expectedResult)
        {
            var result = await client.GetAsync(url);
            if (result.StatusCode != expectedResult)
            {
                Output.WriteLine(await result.Content.ReadAsStringAsync());
            }
            result.StatusCode.Should().Be(expectedResult);
        }

        private Route RouteOne()
        {
            return async context =>
            {
                var request = context.Request;
                if (request.Method != HttpMethods.Get || request.Path != "/test/one")
                    return null;

                return new RouteResult.Complete(HttpResponse.Create());
            };
        }

        private Route RouteTwo()
        {
            return async context =>
            {
                var request = context.Request;
                if (request.Method != HttpMethods.Get || request.Path != "/test/two")
                    return null;

                return new RouteResult.Complete(HttpResponse.Create());
            };
        }
    }
}