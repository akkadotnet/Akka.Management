// -----------------------------------------------------------------------
//  <copyright file="AkkaRouter.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Ceen;

namespace Akka.Http
{
    public class AkkaRouter : IRouter
    {
        private readonly ActorSystem _system;
        private readonly Route _routes;
        private readonly ILoggingAdapter _log;

        public AkkaRouter(ActorSystem system, Route routes)
        {
            _system = system;
            _routes = routes;
            _log = Logging.GetLogger(system, typeof(AkkaRouter));
        }

        public async Task<bool> Process(IHttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            
            var requestContext = new RequestContext(await HttpRequest.CreateAsync(context.Request), _system);
                        
            var response = await _routes(requestContext);
            switch (response)
            {
                case null:
                    context.Response.StatusCode = HttpStatusCode.NotFound;
                    _log.Info($"Request to path {context.Request.Path} rejected: [{HttpStatusCode.NotFound}]");
                    break;
                case RouteResult.Rejected reject:
                    // TODO: Do response error code conversion  
                    switch (reject.Rejection)
                    {
                        default:
                            context.Response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                    }
                    _log.Info($"Request to path {context.Request.Path} rejected: [{reject}]");
                    break;
                case RouteResult.Complete complete:
                    var r = complete.Response;
                    context.Response.StatusCode = r.Status;
                    context.Response.ContentType = r.Entity.ContentType;
                    await context.Response.WriteAllAsync(r.Entity.DataBytes.ToArray());
                    _log.Debug($"Request to path {context.Request.Path} completed successfully.");
                    break;
            }

            return true;
        }
    }
}