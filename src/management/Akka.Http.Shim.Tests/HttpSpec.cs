//-----------------------------------------------------------------------
// <copyright file="HttpSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Settings;
using Akka.Http.Extensions;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Http.Shim.Tests
{
    public class HttpSpec : TestKit.Xunit2.TestKit
    {
        private static readonly Config BaseConfig = 
            ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port = 0");
        
        public HttpSpec(ITestOutputHelper output) : base(BaseConfig, nameof(HttpSpec), output)
        { }
        
        [Fact]
        public async Task Should_Bind_Properly()
        {
            var baseBuilder = Sys.Http()
                .NewServerAt("localhost", 8081)
                .WithSettings(ServerSettings.Create((ExtendedActorSystem)Sys));

            var serverBinding = await baseBuilder.Bind(new (string, HttpModuleBase)[]
            {
                ("/test/one", new RouteHandler()), 
                ("/test/two", new RouteHandler())
            });

            Log.Info($"Bound Akka Management (HTTP) endpoint to: {serverBinding.LocalAddress}");

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

        internal class RouteHandler : HttpModuleBase
        {
            public override Task<bool> HandleAsync(IAkkaHttpContext httpContext)
            {
                var context = httpContext.HttpContext;
                var request = context.Request;
                if (request.Method != "GET")
                    return Task.FromResult(false);
                context.Response.StatusCode = Ceen.HttpStatusCode.OK;
                return Task.FromResult(true);
            }
        }
    }
}