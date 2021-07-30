using System.Collections.Immutable;
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