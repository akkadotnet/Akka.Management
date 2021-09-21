using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Configuration;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.Http.Dsl.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
#if NET5_0
using Microsoft.Extensions.Hosting;
#else
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
#endif

namespace Akka.Http.Dsl
{
    public delegate Task<RouteResult.IRouteResult> Route(RequestContext context);
    public delegate Route RouteGenerator<T>(T obj);
    
    
    public sealed class HttpExt : IExtension
    {
        private readonly ExtendedActorSystem _system;
        private readonly ServerSettings _settings;

        public HttpExt(ExtendedActorSystem system)
        {
            _system = system;
            _system.Settings.InjectTopLevelFallback(Http.DefaultConfig());
            _settings = ServerSettings.Create(_system);
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
        public async Task<ServerBinding> BindAndHandleAsync(
            Route route, 
            string hostname = null, 
            int? port = null, 
            ServerSettings settings = null)
        {
            var effectiveSetting = settings ?? _settings;
            var effectiveHostname = hostname ?? "localhost";
            var effectivePort = port ?? effectiveSetting.DefaultHttpPort;

#if NET5_0
            var host = Host.CreateDefaultBuilder()
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddFilter("Microsoft", LogLevel.Error);
                })
                .ConfigureWebHostDefaults(builder =>
                {
                    builder
                        .UseKestrel()
                        .SuppressStatusMessages(true)
                        .Configure(app =>
                        {
                            // Uncomment for server debugging
                            app.UseDeveloperExceptionPage();

                            // Actual middleware that handles Akka.Http routing and adapts HttpRequest and HttpResponse
                            // between Akka.Http and ASP.NET
                            app.UseAkkaRouting(_system, route, effectiveSetting);
                        })
                        .UseUrls($"http://{effectiveHostname}:{effectivePort}");
                })
                .Build();
#else
            var host = WebHost.CreateDefaultBuilder()
                .SuppressStatusMessages(true)
                .ConfigureServices(services =>
                {
                    services.AddLogging(builder => builder.AddFilter("Microsoft", LogLevel.Error));
                })
                .Configure(app =>
                {
                    // Uncomment for server debugging
                    app.UseDeveloperExceptionPage();

                    // Actual middleware that handles Akka.Http routing and adapts HttpRequest and HttpResponse
                    // between Akka.Http and ASP.NET
                    app.UseAkkaRouting(_system, route, effectiveSetting);
                })
                .UseUrls($"http://{effectiveHostname}:{effectivePort}")
                .Build();
#endif

            // Start listening...
            await host.StartAsync();

            var binding = new ServerBinding(
                new DnsEndPoint(effectiveHostname, effectivePort),
                async timeout =>
                {
                    await host.StopAsync(timeout);
                    return HttpServerTerminated.Instance;
                });
            binding.AddToCoordinatedShutdown(TimeSpan.FromSeconds(5), _system);
            return binding;
        }
    }

    public class Http : ExtensionIdProvider<HttpExt>
    {
        public static Config DefaultConfig()
        {
            return ConfigurationFactory.FromResource<HttpExt>("Akka.Http.Resources.reference.conf");
        }
        
        public new static HttpExt Get(ActorSystem system) => system.WithExtension<HttpExt, Http>();

        public override HttpExt CreateExtension(ExtendedActorSystem system) =>
            new HttpExt(system);
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