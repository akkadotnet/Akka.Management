using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Akka.Actor;
using Akka.Cluster;
using static Akka.Management.Cluster.Bootstrap.ContactPoint.BootstrapProtocol;
namespace Akka.Management.Cluster.Bootstrap.ContactPoint
{
    public class ClusterBootstrapRoute
    {
        private readonly ClusterBootstrapSettings _settings;

        public ClusterBootstrapRoute(ClusterBootstrapSettings settings)
        {
            _settings = settings;
        }

        private SeedNodes GetSeedNodes(ActorSystem system)
        {
            var cluster = Akka.Cluster.Cluster.Get(system);

            ClusterMember MemberToClusterMember(Member m)
                => new ClusterMember(m.UniqueAddress.Address, m.UniqueAddress.Uid, m.Status, m.Roles);

            var state = cluster.State;
            
            // TODO shuffle the members so in a big deployment nodes start joining different ones and not all the same?
            var members = state.Members
                .Where(m => !state.Unreachable.Contains(m))
                .Where(m => m.Status == MemberStatus.Up || 
                            m.Status == MemberStatus.WeaklyUp ||
                            m.Status == MemberStatus.Joining)
                .Take(_settings.ContactPoint.MaxSeedNodesToExpose)
                .Select(MemberToClusterMember).ToImmutableHashSet();

            return new SeedNodes(cluster.SelfMember.UniqueAddress.Address, members);
        }
    }

    public static class ClusterBootstrapRequests
    {
        public static Uri BootstrapSeedNodes(Uri baseUri)
        {
            return new Uri(baseUri + "/bootstrap/seed-nodes");
        }
    }
}