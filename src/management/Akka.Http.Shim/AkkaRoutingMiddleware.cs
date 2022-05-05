//-----------------------------------------------------------------------
// <copyright file="AkkaRoutingMiddleware.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.Http.Dsl.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Akka.Http
{
    public class AkkaRoutingMiddleware
    {
        private readonly ExtendedActorSystem _system;
        private readonly Route _routes;
        private readonly ServerSettings _settings;
        private readonly ILoggingAdapter _log;
        
        internal AkkaRoutingMiddleware(RequestDelegate next, AkkaRoutingOptions options)
        {
            _system = options.System;
            _routes = options.Routes;
            _settings = options.Settings;

            _log = Logging.GetLogger(_system, typeof(AkkaRoutingMiddleware));
        }
        
        /// <summary>
        /// Executes the middleware.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <returns>A task that represents the execution of this middleware.</returns>
        public async Task Invoke(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            
            var requestContext = new RequestContext(await Dsl.Model.HttpRequest.CreateAsync(context.Request), _system);
                        
            var response = await _routes(requestContext);
            switch (response)
            {
                case null:
                    context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                    _log.Info($"Request to path {context.Request.Path} rejected: [{HttpStatusCode.NotFound}]");
                    break;
                case RouteResult.Rejected reject:
                    // TODO: Do response error code conversion  
                    switch (reject.Rejection)
                    {
                        default:
                            context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                            break;
                    }
                    _log.Info($"Request to path {context.Request.Path} rejected: [{reject}]");
                    break;
                case RouteResult.Complete complete:
                    var r = complete.Response;
                    context.Response.StatusCode = r.Status;
                    context.Response.ContentType = r.Entity.ContentType;
                    await context.Response.WriteAsync(r.Entity.DataBytes.ToString());
                    _log.Debug($"Request to path {context.Request.Path} completed successfully.");
                    break;
            }
        }
    }

    internal class AkkaRoutingOptions
    {
        public AkkaRoutingOptions(ExtendedActorSystem system, Route routes, ServerSettings settings)
        {
            System = system;
            Routes = routes;
            Settings = settings;
        }

        public ExtendedActorSystem System { get; }
        public Route Routes { get; }
        public ServerSettings Settings { get; }
    }

    public static class AkkaRoutingExtension
    {
        public static IApplicationBuilder UseAkkaRouting(
            this IApplicationBuilder app,
            ExtendedActorSystem system,
            Route routes,
            ServerSettings settings)
        {
            if (system is null)
                throw new ArgumentNullException(nameof(system));
            if (routes is null)
                throw new ArgumentNullException(nameof(routes));
            if (settings is null)
                throw new ArgumentNullException(nameof(settings));

            var opt = new AkkaRoutingOptions(system, routes, settings);
            return app.Use(next => new AkkaRoutingMiddleware(next, opt).Invoke);
        }
    }
}