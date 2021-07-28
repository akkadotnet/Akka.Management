using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;

namespace Akka.Management.Cluster.Bootstrap
{
    /// <summary>
    /// The decisions of joining existing seed-nodes or join self to form new
    /// cluster is performed by the `JoinDecider` and the implementation is
    /// defined in configuration so support different strategies.
    /// </summary>
    public interface IJoinDecider
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        Task<IJoinDecision> Decide(SeedNodesInformation info);
    }
    
    /// <summary>
    /// Full information about discovered contact points and found seed nodes.
    /// </summary>
    public sealed class SeedNodesInformation
    {
        /// <summary>
        /// Create a new instance of <see cref="SeedNodesInformation"/>
        /// </summary>
        /// <param name="currentTime">TBD</param>
        /// <param name="contactPointsChangedAt">
        /// is when the discovered contact points were last changed (e.g. via DNS lookup),
        /// e.g. 5 seconds ago means that subsequent lookup attempts (1 per second) after that were successful and
        /// returned the same set.
        /// </param>
        /// <param name="contactPoints">
        /// contains all nodes that were returned from the discovery (e.g. DNS lookup).
        /// </param>
        /// <param name="seedNodesObservations">
        /// contains the replies from those contact points when probing them with the HTTP call. It only contains
        /// entries for the contact points that actually replied, i.e. were reachable and running. Each such
        /// <see cref="SeedNodesObservation"/> entry has the <see cref="SeedNodesObservation.SeedNodes"/>
        /// (Akka Cluster addresses) that were returned from that contact point. That `Set` will be
        /// empty if the node replied but is not part of an existing cluster yet, i.e. it hasn't joined.
        ///
        /// There are also some timestamps that can be interesting. Note that `currentTime` is passed in
        /// to facilitate calculation of durations.
        /// </param>
        public SeedNodesInformation(
            DateTime currentTime, 
            DateTime contactPointsChangedAt, 
            ImmutableHashSet<ServiceDiscovery.ResolvedTarget> contactPoints, 
            ImmutableHashSet<SeedNodesObservation> seedNodesObservations)
        {
            CurrentTime = currentTime;
            ContactPointsChangedAt = contactPointsChangedAt;
            ContactPoints = contactPoints;
            SeedNodesObservations = seedNodesObservations;
        }

        public DateTime CurrentTime { get; }
        public DateTime ContactPointsChangedAt { get; }
        public ImmutableHashSet<ServiceDiscovery.ResolvedTarget> ContactPoints { get; }
        public ImmutableHashSet<SeedNodesObservation> SeedNodesObservations { get; }

        public bool HasSeedNodes =>
            !SeedNodesObservations.IsEmpty && SeedNodesObservations.Any(s => !s.SeedNodes.IsEmpty);

        public ImmutableHashSet<Address> AllSeedNodes =>
            SeedNodesObservations.SelectMany(s => s.SeedNodes).ToImmutableHashSet();
    }

    public sealed class SeedNodesObservation
    {
        public SeedNodesObservation(DateTime observedAt, ServiceDiscovery.ResolvedTarget contactPoint, Address sourceAddress, ImmutableHashSet<Address> seedNodes)
        {
            ObservedAt = observedAt;
            ContactPoint = contactPoint;
            SourceAddress = sourceAddress;
            SeedNodes = seedNodes;
        }

        /// <summary>
        /// was when that reply was received from that contact point.
        /// The entry is removed if no reply was received within the `probing-failure-timeout` meaning that it
        /// is unreachable or not running.
        /// </summary>
        public DateTime ObservedAt { get; }
        public ServiceDiscovery.ResolvedTarget ContactPoint { get; }
        public Address SourceAddress { get; }
        public ImmutableHashSet<Address> SeedNodes { get; }
    }

    public interface IJoinDecision
    {
    }

    /// <summary>
    /// Not ready to join yet, continue discovering contact points and retrieve seed nodes.
    /// </summary>
    public sealed class KeepProbing : IJoinDecision
    {
        public static KeepProbing Instance { get; } = new KeepProbing();
        private KeepProbing() { }
    }
    
    /// <summary>
    /// There is no existing cluster running and this node decided to form a new cluster by joining itself.
    /// Other nodes should discover this and join the same.
    /// </summary>
    public sealed class JoinSelf : IJoinDecision
    {
        public static JoinSelf Instance { get; } = new JoinSelf();
        private JoinSelf() { }
    }

    /// <summary>
    /// Join existing cluster.
    ///
    /// The self <see cref="Address"/> will be removed from the returned <see cref="JoinOtherSeedNodes.SeedNodes"/> to
    /// be sure that it's never joining itself via this decision.
    /// </summary>
    public sealed class JoinOtherSeedNodes : IJoinDecision
    {
        public JoinOtherSeedNodes(ImmutableHashSet<Address> seedNodes)
        {
            SeedNodes = seedNodes;
        }

        public ImmutableHashSet<Address> SeedNodes { get; }
    }
}