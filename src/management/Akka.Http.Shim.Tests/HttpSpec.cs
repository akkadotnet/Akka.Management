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
using Akka.Http.Dsl;
using Akka.Http.Dsl.Settings;
using Akka.Http.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using Xunit.Abstractions;
using HttpResponse = Akka.Http.Dsl.Model.HttpResponse;
using static Akka.Http.Dsl.Server.RouteResult;

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
            return context =>
            {
                var request = context.Request;
                if (request.Method != HttpMethods.Get || request.Path != "/test/one")
                    return Task.FromResult<IRouteResult>(null);

                return Task.FromResult<IRouteResult>(new Complete(HttpResponse.Create()));
            };
        }

        private Route RouteTwo()
        {
            return context =>
            {
                var request = context.Request;
                if (request.Method != HttpMethods.Get || request.Path != "/test/two")
                    return Task.FromResult<IRouteResult>(null);

                return Task.FromResult<IRouteResult>(new Complete(HttpResponse.Create())); 
            };
        }
    }
}