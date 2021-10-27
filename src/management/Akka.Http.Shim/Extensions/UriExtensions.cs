//-----------------------------------------------------------------------
// <copyright file="UriExtensions.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

namespace System
{
    public static class UriExtensions
    {
        public static Uri WithPort(this Uri uri, int newPort)
        {
            var builder = new UriBuilder(uri) { Port = newPort };
            return builder.Uri;
        }
    }
}