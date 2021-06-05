using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Configuration;
using Akka.Http.Dsl.Settings;
using Akka.Http.Internal;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Akka.Http.Dsl
{
    using HttpRequest = Model.HttpRequest;
    using HttpResponse = Model.HttpResponse;

    public sealed class HttpExt : IExtension
    {
        private readonly Config _config;
        private readonly ExtendedActorSystem _system;

        public HttpExt(Config config, ExtendedActorSystem system)
        {
            _config = config;
            _system = system;
        }

        /// <summary>
        /// Main entry point to create a server binding.
        /// </summary>
        /// <param name="hostname">The interface to bind to.</param>
        /// <param name="port">The port to bind to or `0` if the port should be automatically assigned.</param>
        public ServerBuilder NewServerAt(string hostname, int port) =>
            ServerBuilder.Create(hostname, port, _system);

        /// <summary>
        /// Convenience method which starts a new HTTP server at the given endpoint and uses the given `handler`
        /// for processing all incoming connections.
        /// </summary>
        public async Task<ServerBinding> BindAndHandleAsync(Func<HttpRequest, Task<HttpResponse>> handler, string hostname, int port, ServerSettings settings)
        {
            var host = WebHost.CreateDefaultBuilder()
                .SuppressStatusMessages(true)
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder => builder.AddFilter("Microsoft", LogLevel.None));
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    static bool Predicate(HttpContext context) =>
                        context.Request.Path.StartsWithSegments("", out var remaining); // && string.IsNullOrEmpty(remaining);

                    app.UseMiddleware<HttpRequestMiddleware>();
                    
                    // Actual middleware that turns an Akka HttpResponse into an AspNetCore HttpResponse
                    app.MapWhen(Predicate, b => b.Run(async context =>
                    {
                        var response = await handler(context.Features.Get<HttpRequest>());
                        if (response != null)
                        {
                            context.Response.StatusCode = response.Status;
                            context.Response.ContentType = response.Entity.ContentType;
                            await context.Response.WriteAsync(response.Entity.DataBytes.ToString());
                        }
                    }));

                    // TODO: for debugging purposes only, remove
                    app.Run(context => context.Response.WriteAsync("Akka-Http middleware seems to be ready."));
                })
                .UseUrls($"http://{hostname}:{port}")
                .Build();

            // Start listening...
            await host.StartAsync();

            return new ServerBinding(
                new DnsEndPoint(hostname, port),
                async timeout =>
                {
                    await host.StopAsync(timeout);
                    return HttpServerTerminated.Instance;
                });
        }
    }

    public class Http : ExtensionIdProvider<HttpExt>
    {
        public new static HttpExt Get(ActorSystem system) => system.WithExtension<HttpExt, Http>();

        public override HttpExt CreateExtension(ExtendedActorSystem system) =>
            new HttpExt(system.Settings.Config.GetConfig("akka.http"), system);
    }

    /// <summary>
    /// Type used to carry meaningful information when server termination has completed successfully.
    /// </summary>
    [DoNotInherit]
    public abstract class HttpTerminated
    {
    }

    /// <summary>
    /// TBD
    /// </summary>
    public sealed class HttpServerTerminated : HttpTerminated
    {
        public static readonly HttpServerTerminated Instance = new HttpServerTerminated();

        private HttpServerTerminated()
        {
        }
    }
}