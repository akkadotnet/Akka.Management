//-----------------------------------------------------------------------
// <copyright file="Http.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Configuration;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Settings;
using Ceen.Httpd;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Http
{
    public sealed class HttpExt : IExtension
    {
        private readonly ExtendedActorSystem _system;
        private readonly ServerSettings _settings;
        private readonly ILoggingAdapter _log;
        private readonly CancellationTokenSource _shutdownCts;
        private Task? _serverTask;

        public HttpExt(ExtendedActorSystem system)
        {
            _system = system;
            _system.Settings.InjectTopLevelFallback(Http.DefaultConfig());
            _settings = ServerSettings.Create(_system);
            _log = Logging.GetLogger(system, this);
            _shutdownCts = new CancellationTokenSource();
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
            Route[] routes, 
            string? hostname = null, 
            int? port = null, 
            ServerSettings? settings = null)
        {
            var effectiveSetting = settings ?? _settings;
            var effectiveHostname = hostname ?? "localhost";
            var effectivePort = port ?? effectiveSetting.DefaultHttpPort;

            var config = new ServerConfig
            {
                Router = new AkkaRouter(_system, routes)
            };

            // Start listening...
            if (!IPAddress.TryParse(effectiveHostname, out var ip))
            {
                var addresses = await Dns.GetHostAddressesAsync(effectiveHostname);
                ip = addresses.First(i => i.AddressFamily == AddressFamily.InterNetwork && !Equals(i, IPAddress.Any));
            }
            var endpoint = new IPEndPoint(ip, effectivePort);
            
            _serverTask = HttpServer.ListenAsync(
                endpoint,
                false,
                config,
                _shutdownCts.Token);

            if (_serverTask.IsFaulted)
            {
                _serverTask.GetAwaiter().GetResult();
            }
            
            _log.Info("HTTP Extension started");

            var binding = new ServerBinding(
                endpoint,
                async timeout =>
                {
                    _shutdownCts.Cancel();
                    using (var cts = new CancellationTokenSource(timeout))
                    {
                        await Task.WhenAny(Task.Delay(Timeout.Infinite, cts.Token), _serverTask);
                    }
                    _log.Info("HTTP Extension stopped");
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