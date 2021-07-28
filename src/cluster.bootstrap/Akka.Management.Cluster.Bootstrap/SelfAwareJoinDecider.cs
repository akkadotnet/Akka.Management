using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;
using Akka.Event;

namespace Akka.Management.Cluster.Bootstrap
{
    public abstract class SelfAwareJoinDecider : IJoinDecider
    {
        private readonly ActorSystem _system;
        
        protected ILoggingAdapter Log { get; }
        
        protected SelfAwareJoinDecider(ActorSystem system, ClusterBootstrapSettings settings)
        {
            Settings = settings;
            _system = system;
            Log = Logging.GetLogger(_system, typeof(SelfAwareJoinDecider));
        }
        
        public ClusterBootstrapSettings Settings { get; } 

        protected string ContactPointString((string host, int port) contactPoint)
            => $"{contactPoint.host}:{contactPoint.port}";

        protected string ContactPointString(ServiceDiscovery.ResolvedTarget contactPoint)
            => $"{contactPoint.Host}:{(contactPoint.Port ?? 0)}";

        public (string host, int port) SelfContactPoint
        {
            get
            {
                var selfAddress = Akka.Cluster.Cluster.Get(_system).SelfAddress;
                return (selfAddress.Host, selfAddress.Port ?? 0);
            }
        }

        public bool CanJoinSelf(ServiceDiscovery.ResolvedTarget target, SeedNodesInformation info)
        {
            var self = SelfContactPoint;
            if (MatchesSelf(target, self))
                return true;
            
            if (!info.ContactPoints.Any(t => MatchesSelf(t, self)))
            {
                Log.Warning("Self contact point [{}] not found in targets {}",
                    ContactPointString(self),
                    string.Join(", ", info.ContactPoints));
            }
            return false;
        }

        public bool MatchesSelf(ServiceDiscovery.ResolvedTarget target, (string host, int port) contactPoint)
        {
            if (target.Port == null)
                return HostMatches(contactPoint.host, target);
            return HostMatches(contactPoint.host, target) && contactPoint.port == target.Port;
        }

        public bool HostMatches(string host, ServiceDiscovery.ResolvedTarget target)
            => host == target.Host || target.Address.ToString().Contains(host.Replace("\\", ""));

        public abstract Task<IJoinDecision> Decide(SeedNodesInformation info);
    }
}