using Akka.Actor;

namespace Akka.Http.Dsl
{
    public static class HttpExtExtensions
    {
        public static HttpExt Http(this ActorSystem system) => system.WithExtension<HttpExt, Http>();
    }
}
