//-----------------------------------------------------------------------
// <copyright file="LowestAddressJoinDecider.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;
using Akka.Event;
using Akka.Management.Cluster.Bootstrap.Util;

namespace Akka.Management.Cluster.Bootstrap
{
    public class LowestAddressJoinDecider : SelfAwareJoinDecider
    {
        public LowestAddressJoinDecider(ActorSystem system, ClusterBootstrapSettings settings) : base(system, settings)
        {
        }

        public override Task<IJoinDecision> Decide(SeedNodesInformation info)
        {
            if (info.HasSeedNodes)
            {
                var seeds = JoinOtherSeedNodes(info);
                if (seeds.IsEmpty)
                    return Task.FromResult((IJoinDecision)KeepProbing.Instance);
                return Task.FromResult((IJoinDecision)new JoinOtherSeedNodes(seeds));
            } 
            
            if (!HasEnoughContactPoints(info))
            {
                Log.Info(
                    "Discovered [{0}] contact points, confirmed [{1}], which is less than the required [{2}], retrying",
                    info.ContactPoints.Count,
                    info.SeedNodesObservations.Count,
                    Settings.ContactPointDiscovery.RequiredContactPointsNr);
                return Task.FromResult((IJoinDecision)KeepProbing.Instance);
            }

            if (!IsPastStableMargin(info))
            {
                Log.Debug("Contact points observations have changed more recently than the stable-margin [{0}], changed at [{1}], " +
                          "not joining myself. This process will be retried.",
                    Settings.ContactPointDiscovery.StableMargin,
                    info.ContactPointsChangedAt);
                return Task.FromResult((IJoinDecision)KeepProbing.Instance);
            }
            
            // No seed nodes
            var contactPointsWithoutSeedNodesObservation = ImmutableHashSet<ServiceDiscovery.ResolvedTarget>.Empty;

            if (IsConfirmedCommunicationWithAllContactPointsRequired(info))
            {
                var builder = info.ContactPoints.ToBuilder();
                foreach (var contact in info.SeedNodesObservations.Select(o => o.ContactPoint))
                {
                    builder.Remove(contact);
                }
                contactPointsWithoutSeedNodesObservation = builder.ToImmutableHashSet();
            }

            if (contactPointsWithoutSeedNodesObservation.IsEmpty)
            {
                // got info from all contact points as expected
                var lowestAddress = LowestAddressContactPoint(info);
                // can the lowest address, if exists, join self
                var canJoinSelf = lowestAddress != null &&  CanJoinSelf(lowestAddress, info);
                if (canJoinSelf && Settings.NewClusterEnabled)
                {
                    return Task.FromResult((IJoinDecision)JoinSelf.Instance);
                }

                if (Settings.NewClusterEnabled)
                {
                    if (Log.IsInfoEnabled)
                        Log.Info(
                            "Exceeded stable margins without locating seed-nodes, however this node {0} is NOT the lowest address " +
                            "out of the discovered endpoints in this deployment, thus NOT joining self. Expecting node [{1}] " +
                            "(out of [{2}]) to perform the self-join and initiate the cluster.",
                            ContactPointString(SelfContactPoint()),
                            lowestAddress == null ? "" : ContactPointString(lowestAddress),
                            string.Join(", ", info.ContactPoints.Select(ContactPointString)));
                }
                else
                {
                    if(Log.IsWarningEnabled)
                        Log.Warning("Exceeded stable margins without locating seed-nodes, however this node {0} is configured with " +
                                    "new-cluster-enabled=off, thus NOT joining self. Expecting existing cluster or node [{1}] " +
                                    "(out of [{2}]) to perform the self-join and initiate the cluster.",
                            ContactPointString(SelfContactPoint()),
                            lowestAddress == null ? "" : ContactPointString(lowestAddress),
                            string.Join(", ", info.ContactPoints.Select(ContactPointString)));
                }
                
                // the probing will continue until the lowest addressed node decides to join itself.
                // note, that due to DNS changes this may still become this node! We'll then await until the dns stableMargin
                // is exceeded and would decide to try joining self again (same code-path), that time successfully though.
                return Task.FromResult((IJoinDecision)KeepProbing.Instance);
            }
            
            // missing info from some contact points (e.g. because of probe failing)
            var contactPointsWithoutSeedNodesObservations = info.SeedNodesObservations
                .Select(n => n.ContactPoint)
                .Aggregate(info.ContactPoints, (current, target) => current.Remove(target));
            if(Log.IsInfoEnabled)
                Log.Info("Exceeded stable margins but missing seed node information from some contact points [{0}] (out of [{1}])",
                    string.Join(", ", contactPointsWithoutSeedNodesObservations.Select(ContactPointString)),
                    string.Join(", ", info.ContactPoints.Select(ContactPointString)));
            
            return Task.FromResult((IJoinDecision)KeepProbing.Instance);
        }
        
        /// <summary>
        /// May be overridden by subclass to extract the nodes to use as seed nodes when joining
        /// existing cluster. `info.allSeedNodes` contains all existing nodes.
        /// If the returned `Set` is empty it will continue probing.
        /// </summary>
        protected virtual ImmutableHashSet<Address> JoinOtherSeedNodes(SeedNodesInformation info)
            => info.AllSeedNodes.Take(5).ToImmutableHashSet();
        
        /// <summary>
        /// May be overridden by subclass to decide if enough contact points have been discovered.
        /// `info.contactPoints.size` is the number of discovered (e.g. via DNS lookup) contact points
        /// and `info.seedNodesObservations.size` is the number that has been confirmed that they are
        /// reachable and running.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        protected virtual bool HasEnoughContactPoints(SeedNodesInformation info)
            => info.SeedNodesObservations.Count >= Settings.ContactPointDiscovery.RequiredContactPointsNr;

        /// <summary>
        /// May be overridden by subclass to decide if the set of discovered contact points is stable.
        /// `info.contactPointsChangedAt` was the time when the discovered contact points were changed
        /// last time. Subsequent lookup attempts after that returned the same contact points.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        protected virtual bool IsPastStableMargin(SeedNodesInformation info)
        {
            var contactPointsChanged = info.CurrentTime - info.ContactPointsChangedAt;
            return contactPointsChanged.TotalMilliseconds >=
                   Settings.ContactPointDiscovery.StableMargin.TotalMilliseconds;
        }

        /// <summary>
        /// May be overridden by subclass to allow joining self even though some of the discovered
        /// contact points have not been confirmed (unreachable or not running).
        /// `hasEnoughContactPoints` and `isPastStableMargin` must still be fulfilled.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        protected virtual bool IsConfirmedCommunicationWithAllContactPointsRequired(SeedNodesInformation info)
            => Settings.ContactPointDiscovery.ContactWithAllContactPoints;

        /// <summary>
        /// May be overridden by subclass for example if another sort order is desired.
        ///
        /// Contact point with the "lowest" contact point address,
        /// it is expected to join itself if no other cluster is found in the deployment.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        protected virtual ServiceDiscovery.ResolvedTarget? LowestAddressContactPoint(SeedNodesInformation info)
        {
            // Note that we are using info.seedNodesObservations and not info.contactPoints here, but that
            // is the same when isConfirmedCommunicationWithAllContactPointsRequired == true
            var list = info.SeedNodesObservations.Select(o => o.ContactPoint).ToList();
            list.Sort(ResolvedTargetComparer.Instance);
            return list.Count == 0 ? null : list[0];
        }
    }
}