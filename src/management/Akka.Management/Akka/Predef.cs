//-----------------------------------------------------------------------
// <copyright file="Predef.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

namespace Akka
{
    public static class Predef
    {
        /// <summary>
        /// Identity function to conform with the JVM API
        /// </summary>
        public static T Identity<T>(T x) => x;
    }
}