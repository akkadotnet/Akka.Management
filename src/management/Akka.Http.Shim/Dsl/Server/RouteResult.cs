//-----------------------------------------------------------------------
// <copyright file="RouteResult.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Http.Dsl.Model;

namespace Akka.Http.Dsl.Server
{
    public static class RouteResult
    {
        public interface IRouteResult { }
        
        public sealed class Complete : IRouteResult
        {
            public Complete(HttpResponse response)
            {
                Response = response;
            }

            public HttpResponse Response { get; }
        }
        
        public sealed class Rejected : IRouteResult
        {
            public Rejected(IRejection rejection)
            {
                Rejection = rejection;
            }

            public IRejection Rejection { get; }
        }
    }
}