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