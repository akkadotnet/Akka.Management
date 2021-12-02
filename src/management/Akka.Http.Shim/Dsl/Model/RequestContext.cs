//-----------------------------------------------------------------------
// <copyright file="RequestContext.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;

namespace Akka.Http.Dsl.Model
{
    public class RequestContext
    {
        public RequestContext(HttpRequest request, ActorSystem actorSystem)
        {
            Request = request;
            ActorSystem = actorSystem;
        }

        public HttpRequest Request { get; }
        public ActorSystem ActorSystem { get; }
    }
}