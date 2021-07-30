using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Configuration;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.Http.Dsl.Settings;
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

    public delegate Task<RouteResult.IRouteResult> Route(RequestContext context);
    public delegate Route RouteGenerator<T>(T obj);
    
    
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
        public async Task<ServerBinding> BindAndHandleAsync(Route route, string hostname, int port, ServerSettings settings)
        {
            var localRoute = route;
            var host = WebHost.CreateDefaultBuilder()
                .SuppressStatusMessages(true)
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder => builder.AddFilter("Microsoft", LogLevel.Debug));
                    services.AddRouting();
                })
                .Configure(app =>
                {
                    static bool Predicate(HttpContext context) =>
                        context.Request.Path.StartsWithSegments("", out var remaining); // && string.IsNullOrEmpty(remaining);

                    // Uncomment for server debugging
                    //app.UseDeveloperExceptionPage();
                    
                    // Actual middleware that turns an Akka HttpResponse into an AspNetCore HttpResponse
                    app.MapWhen(Predicate, b => b.Run(async context =>
                    {
                        var requestContext = new RequestContext(HttpRequest.Create(context.Request), _system);
                        
                        var response = await localRoute(requestContext);
                        switch (response)
                        {
                            case null:
                                context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                                break;
                            case RouteResult.Rejected _:
                                context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                                break;
                            case RouteResult.Complete complete:
                                var r = complete.Response;
                                context.Response.StatusCode = r.Status;
                                context.Response.ContentType = r.Entity.ContentType;
                                await context.Response.WriteAsync(r.Entity.DataBytes.ToString());
                                break;
                        }
                    }));

                    // TODO: for debugging purposes only, remove
                    // app.Run(context => context.Response.WriteAsync("Akka-Http middleware seems to be ready."));
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