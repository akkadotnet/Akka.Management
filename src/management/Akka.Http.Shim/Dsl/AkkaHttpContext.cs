// -----------------------------------------------------------------------
//  <copyright file="IAkkaHttpContext.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Annotations;
using Ceen;

namespace Akka.Http.Dsl;

[InternalApi]
public interface IAkkaHttpContext
{
    ActorSystem ActorSystem { get; }
    IHttpContext HttpContext { get; }
}

[InternalApi]
public class AkkaHttpContext : IAkkaHttpContext
{
    public AkkaHttpContext(ActorSystem system, IHttpContext httpContext)
    {
        ActorSystem = system;
        HttpContext = httpContext;
    }

    public ActorSystem ActorSystem { get; }
    public IHttpContext HttpContext { get; }
}