// -----------------------------------------------------------------------
//  <copyright file="IAkkaHttpModule.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Annotations;
using Ceen;

namespace Akka.Http.Dsl;

[InternalApi]
public interface IAkkaHttpModule: IHttpModule
{
    Task<bool> HandleAsync(IAkkaHttpContext httpContext);
}

[InternalApi]
public abstract class HttpModuleBase : IAkkaHttpModule
{
    public virtual Task<bool> HandleAsync(IHttpContext httpContext)
    {
        throw new System.NotImplementedException();
    }

    public abstract Task<bool> HandleAsync(IAkkaHttpContext httpContext);
}