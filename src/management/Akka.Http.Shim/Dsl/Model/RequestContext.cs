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