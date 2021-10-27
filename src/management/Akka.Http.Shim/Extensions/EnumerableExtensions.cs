//-----------------------------------------------------------------------
// <copyright file="EnumerableExtensions.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Akka.Http.Dsl;

namespace Akka.Http.Extensions
{
    public static class EnumerableExtensions
    {
        public static Route Concat(this IEnumerable<Route> routes)
        {
            var routeArray = routes.ToArray();
            return async context =>
            {
                foreach (var route in routeArray)
                {
                    var result = await route(context);
                    if (result != null)
                        return result;
                }

                return null;
            };
        }
    }
}