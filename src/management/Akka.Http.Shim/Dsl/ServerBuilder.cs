using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Settings;

namespace Akka.Http.Dsl
{
    /// <summary>
    /// <para>Builder API to create server bindings.</para>
    /// <para>Use <see cref="HttpExt.NewServerAt"/> to create a new server builder, use methods to customize settings,
    /// and then call one of the bind* methods to bind a server.</para>
    /// </summary>
    public sealed class ServerBuilder
    {
        private readonly ActorSystem _system;
        private readonly HttpExt _http;

        public string Hostname { get; }
        public int Port { get; }
        public ILoggingAdapter Log { get; }
        public ServerSettings Settings { get; }

        internal static ServerBuilder Create(string hostname, int port, ExtendedActorSystem system) =>
            new ServerBuilder(hostname, port, system.Log, ServerSettings.Create(system), system);

        private ServerBuilder(string hostname, int port, ILoggingAdapter log, ServerSettings settings, ActorSystem system)
        {
            _system = system;
            _http = system.Http();

            Hostname = hostname;
            Port = port;
            Log = log;
            Settings = settings;
        }

        /// <summary>
        /// Change interface to bind to
        /// </summary>
        public ServerBuilder OnInterface(string newInterface) => Copy(newInterface);

        /// <summary>
        /// Change port to bind to
        /// </summary>
        public ServerBuilder OnPort(int newPort) => Copy(port: newPort);

        /// <summary>
        /// Use a custom logger
        /// </summary>
        public ServerBuilder LogTo(ILoggingAdapter log) => Copy(log: log);

        /// <summary>
        /// Use custom <see cref="ServerSettings"/> for the binding.
        /// </summary>
        public ServerBuilder WithSettings(ServerSettings settings) => Copy(settings: settings);

        /// <summary>
        /// Bind a new HTTP server and use the given asynchronous `handler` for processing all incoming connections.
        /// </summary>
        public Task<ServerBinding> Bind(Func<HttpRequest, Task<HttpResponse>> handler) =>
            _http.BindAndHandleAsync(handler, Hostname, Port, Settings);
        
        /// <summary>
        /// Bind a new HTTP server at the given endpoint and uses the given `handler` for processing all incoming connections.
        /// </summary>
        public Task<ServerBinding> BindSync(Func<HttpRequest, HttpResponse> handler) =>
            _http.BindAndHandleAsync(req => Task.FromResult(handler(req)), Hostname, Port, Settings);

        private ServerBuilder Copy(
            string hostname = null,
            int? port = null,
            ILoggingAdapter log = null,
            ServerSettings settings = null,
            ExtendedActorSystem system = null) => new ServerBuilder(hostname ?? Hostname, port ?? Port, log ?? Log, settings ?? Settings, system ?? _system);
    }
}