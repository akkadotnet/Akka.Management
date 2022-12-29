//-----------------------------------------------------------------------
// <copyright file="ActorSystemExtensions.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;

namespace Akka.Http
{
    public static class HttpExtExtensions
    {
        public static HttpExt Http(this ActorSystem system) => system.WithExtension<HttpExt, Http>();
    }
}
