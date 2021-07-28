using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Discovery;
using Akka.Event;
using Akka.Util;
using static Akka.Discovery.ServiceDiscovery;

namespace Akka.Management.Cluster.Bootstrap.Internal
{
    public class BootstrapCoordinator : ReceiveActor, IWithTimers
    {
        public static Props Props(
            ServiceDiscovery discovery,
            IJoinDecider joinDecider,
            ClusterBootstrapSettings settings)
            => Actor.Props.Create(() => new BootstrapCoordinator(discovery, joinDecider, settings));
        
        public static class Protocol
        {
            public sealed class InitiateBootstrapping
            {
                public static readonly InitiateBootstrapping Instance = new InitiateBootstrapping();
                private InitiateBootstrapping() { }
            }
        }
        
        private sealed class DiscoverTick : IDeadLetterSuppression
        {
            public static readonly DiscoverTick Instance = new DiscoverTick();
            private DiscoverTick() { }
        }
        
        private sealed class DecideTick : IDeadLetterSuppression
        {
            public static readonly DecideTick Instance = new DecideTick();
            private DecideTick() { }
        }
        
        protected sealed class ServiceContactsObservation
        {
            public ServiceContactsObservation(DateTime observedAt, ImmutableHashSet<ResolvedTarget> observedContactPoints)
            {
                ObservedAt = observedAt;
                ObservedContactPoints = observedContactPoints;
            }

            public DateTime ObservedAt { get; }
            public ImmutableHashSet<ResolvedTarget> ObservedContactPoints { get; }

            public bool MembersChanged(ServiceContactsObservation other)
                => !ObservedContactPoints.Equals(other.ObservedContactPoints);

            public ServiceContactsObservation SameOrChanged(ServiceContactsObservation other)
                => MembersChanged(other) ? other : this;
        }
        
        public ITimerScheduler Timers { get; set; }

        private static IImmutableList<ResolvedTarget> SelectHosts(
            Lookup lookup,
            int fallbackPort,
            bool filterOnFallbackPort,
            IImmutableList<ResolvedTarget> contactPoints)
        {
            // if the user has specified a port name in the search, don't do any filtering and assume it
            // is handled in the service discovery mechanism
            if (!string.IsNullOrEmpty(lookup.PortName) || !filterOnFallbackPort)
                return contactPoints;

            return contactPoints.GroupBy(t => t.Host).SelectMany(g =>
            {
                if (g.ToList().Count == 1)
                    return g;
                if (g.Any(a => a.Port.HasValue))
                    return g.Where(a => a.Port != null && a.Port.Value == fallbackPort);
                return g;
            }).ToImmutableList();
        }

        private readonly ServiceDiscovery _discovery;
        private readonly IJoinDecider _joinDecider;
        private readonly ClusterBootstrapSettings _settings;

        private readonly ILoggingAdapter _log;
        private readonly Akka.Cluster.Cluster _cluster;
        private const string DiscoveryTimerKey = "resolve-key";
        private const string DecideTimerKey = "decide-key";

        private readonly Lookup _lookup;

        private ServiceContactsObservation _lastContactObservation;
        private ImmutableDictionary<ResolvedTarget, SeedNodesObservation> _seedNodesObservations 
            = ImmutableDictionary<ResolvedTarget, SeedNodesObservation>.Empty;
        private bool _decisionInProgress;
        private int _discoveryFailedBackoffCounter;

        public BootstrapCoordinator(ServiceDiscovery discovery, IJoinDecider joinDecider, ClusterBootstrapSettings settings)
        {
            _discovery = discovery;
            _joinDecider = joinDecider;
            _settings = settings;

            _log = Context.GetLogger();
            _cluster = Akka.Cluster.Cluster.Get(Context.System);
            _lookup = new Lookup(
                _settings.ContactPointDiscovery.EffectiveName(Context.System),
                _settings.ContactPointDiscovery.PortName,
                _settings.ContactPointDiscovery.Protocol);
            
            Become(Receive());
        }

        private void StartPeriodicDecisionTimer()
            => Timers.StartPeriodicTimer(DecideTimerKey, DecideTick.Instance, _settings.ContactPoint.ProbeInterval);

        private void ResetDiscoveryInterval()
            => _discoveryFailedBackoffCounter = 0;

        private void BackoffDiscoveryInterval()
            => _discoveryFailedBackoffCounter++;

        private TimeSpan BackedOffInterval(
            int restartCount,
            TimeSpan minBackoff,
            TimeSpan maxBackoff,
            double randomFactor)
        {
            try
            {
                var rnd = 1.0 + ThreadLocalRandom.Current.NextDouble() * randomFactor;
                var ticks = minBackoff.Ticks * Math.Pow(2, restartCount);
                ticks = Math.Min(maxBackoff.Ticks, ticks) * rnd;
                return new TimeSpan((long) ticks);
            }
            catch
            {
                return maxBackoff;
            }
        }

        private void StartSingleDiscoveryTimer()
        {
            var interval = BackedOffInterval(
                _discoveryFailedBackoffCounter,
                _settings.ContactPointDiscovery.Interval,
                _settings.ContactPointDiscovery.ExponentialBackoffMax,
                _settings.ContactPointDiscovery.ExponentialBackoffRandomFactor);
            Timers.StartSingleTimer(DiscoveryTimerKey, DiscoverTick.Instance, interval);
        }

        protected override void PreStart()
        {
            base.PreStart();
            StartSingleDiscoveryTimer();
            StartPeriodicDecisionTimer();
        }

        private Receive Receive()
        {
            return message =>
            {
                switch (message)
                {
                    case Protocol.InitiateBootstrapping _:
                        _log.Info("Locating service members. Using discovery [{0}], join decider [{1}]",
                            _discovery.GetType().Name,
                            _joinDecider.GetType().Name
                        );
                        DiscoverContactPoints();
                        Context.Become(Bootstrapping(Sender));
                        return true;
                    default:
                        return false;
                }
            };
        }

        private Receive Bootstrapping(IActorRef replyTo)
        {
            return message =>
            {
                switch (message)
                {
                    case DiscoverTick _:
                        // the next round of discovery will be performed once this one returns
                        DiscoverContactPoints();
                        return true;

                    case Resolved resolved:
                        var contactPoints = resolved.Addresses;
                        var filteredContactPoints = SelectHosts(
                            _lookup,
                            _settings.ContactPoint.FallbackPort,
                            _settings.ContactPoint.FilterOnFallbackPort,
                            contactPoints);

                        _log.Info("Located service members based on: [{0}]: [{1}], filtered to [{2}]",
                            _lookup,
                            string.Join(", ", contactPoints),
                            string.Join(", ", FormatContactPoints(filteredContactPoints))
                        );
                        OnContactPointsResolved(filteredContactPoints);
                        ResetDiscoveryInterval(); // in case we were backed-off, we reset back to healthy intervals
                        StartSingleDiscoveryTimer(); // keep looking in case other nodes join the discovery
                        return true;

                    case Failure ex:
                        _log.Warning(ex.Exception, "Resolve attempt failed! Cause: {0}", ex.Exception);
                        _lastContactObservation = null;
                        BackoffDiscoveryInterval();
                        StartSingleDiscoveryTimer();
                        return true;

                    case DecideTick _:
                        Decide();
                        return true;

                    case IJoinDecision d:
                        _decisionInProgress = false;
                        switch (d)
                        {
                            case KeepProbing _:
                                // continue scheduled lookups and probing of discovered contact points
                                break;

                            case JoinOtherSeedNodes j:
                                var seedNodes = j.SeedNodes;
                                if (!seedNodes.IsEmpty)
                                {
                                    _log.Info("Joining [{0}] to existing cluster [{1}]",
                                        _cluster.SelfAddress,
                                        string.Join(", ", seedNodes));
                                    var seedNodeList =
                                        seedNodes.Remove(_cluster.SelfAddress).ToList(); // order doesn't matter
                                    _cluster.JoinSeedNodes(seedNodeList);

                                    // once we issued a join bootstrapping is completed
                                    Context.Stop(Self);
                                }

                                break;

                            case JoinSelf _:
                                _log.Info(
                                    "Initiating new cluster, self-joining [{0}]. " +
                                    "Other nodes are expected to locate this cluster via continued contact-point probing.",
                                    _cluster.SelfAddress);
                                _cluster.Join(_cluster.SelfAddress);
                                // once we issued a join bootstrapping is completed
                                Context.Stop(Self);
                                break;
                        }

                        return true;

                    default:
                        return false;
                }
            };
        }

        private IEnumerable<string> FormatContactPoints(IEnumerable<ResolvedTarget> filteredContactPoints)
            => filteredContactPoints.Select(r => $"{r.Host}:{r.Port?.ToString() ?? "0"}");

        private void DiscoverContactPoints()
        {
            _log.Info("Looking up [{}]", _lookup);
            _discovery.Lookup(_lookup, _settings.ContactPointDiscovery.ResolveTimeout).PipeTo(Self);
        }

        private void OnContactPointsResolved(IEnumerable<ResolvedTarget> contactPoints)
        {
            var newObservation = new ServiceContactsObservation(DateTime.Now, contactPoints.ToImmutableHashSet());
            _lastContactObservation = _lastContactObservation != null
                ? _lastContactObservation.SameOrChanged(newObservation)
                : newObservation;
            
            // remove observations from contact points that are not included any more
            _seedNodesObservations = _seedNodesObservations
                .Where(kvp => newObservation.ObservedContactPoints.Contains(kvp.Key)).ToImmutableDictionary();
        }

        private void Decide()
        {
            if (_decisionInProgress)
            {
                _log.Debug("Previous decision still in progress.");
                return;
            }

            if (_lastContactObservation == null)
                return;

            var contacts = _lastContactObservation;
            var currentTime = DateTime.Now;
            
            // filter out old observations, in case the probing failures are not triggered
            bool IsObsolete(SeedNodesObservation obs)
                => (currentTime - obs.ObservedAt).TotalMilliseconds >
                   _settings.ContactPoint.ProbingFailureTimeout.TotalMilliseconds;

            var seedObservations = _seedNodesObservations
                .Select(kvp => kvp.Value)
                .Where(obs => !IsObsolete(obs)).ToImmutableHashSet();
            
            var info = 
                new SeedNodesInformation(currentTime, contacts.ObservedAt, contacts.ObservedContactPoints, seedObservations);

            _decisionInProgress = true;

            _joinDecider.Decide(info).ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    _log.Error(task.Exception, "Join decision failed: {0}", task.Exception);
                    Self.Tell(KeepProbing.Instance);
                    return;
                }
                Self.Tell(task.Result);
            });
        }
    }
}