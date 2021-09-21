using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
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
                public InitiateBootstrapping(Uri selfContactPoint)
                {
                    SelfContactPoint = selfContactPoint;
                }

                public Uri SelfContactPoint { get; }
            }
            
            public sealed class ObtainedSeedNodesObservation : IDeadLetterSuppression
            {
                public ObtainedSeedNodesObservation(
                    DateTimeOffset observedAt, 
                    ResolvedTarget contactPoint, 
                    Address seedNodesSourceAddress, 
                    ImmutableHashSet<Address> observedSeedNodes)
                {
                    ObservedAt = observedAt;
                    ContactPoint = contactPoint;
                    SeedNodesSourceAddress = seedNodesSourceAddress;
                    ObservedSeedNodes = observedSeedNodes;
                }

                public DateTimeOffset ObservedAt { get; }
                public ResolvedTarget ContactPoint { get; }
                public Address SeedNodesSourceAddress { get; }
                public ImmutableHashSet<Address> ObservedSeedNodes { get; }
            }
            
            public sealed class ProbingFailed : IDeadLetterSuppression
            {
                public ProbingFailed(ResolvedTarget contactPoint, Exception cause)
                {
                    ContactPoint = contactPoint;
                    Cause = cause;
                }

                public ResolvedTarget ContactPoint { get; }
                public Exception Cause { get; }
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
            public ServiceContactsObservation(DateTimeOffset observedAt, ImmutableHashSet<ResolvedTarget> observedContactPoints)
            {
                ObservedAt = observedAt;
                ObservedContactPoints = observedContactPoints;
            }

            public DateTimeOffset ObservedAt { get; }
            public ImmutableHashSet<ResolvedTarget> ObservedContactPoints { get; }

            public bool MembersChanged(ServiceContactsObservation other)
            {
                if (ObservedContactPoints.Count != other.ObservedContactPoints.Count)
                    return true;
                
                foreach (var contact in other.ObservedContactPoints)
                {
                    if (!ObservedContactPoints.Contains(contact))
                        return true;
                }

                return false;
            }

            public ServiceContactsObservation SameOrChanged(ServiceContactsObservation other)
                => MembersChanged(other) ? other : this;
        }
        
        public ITimerScheduler Timers { get; set; }

        internal static IImmutableList<ResolvedTarget> SelectHosts(
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

        protected override void PostStop()
        {
            Timers.CancelAll();
            foreach (var child in Context.GetChildren())
            {
                child.Tell(PoisonPill.Instance);
            }
            base.PostStop();
        }

        private Receive Receive()
        {
            return message =>
            {
                switch (message)
                {
                    case Protocol.InitiateBootstrapping msg:
                        var scheme = msg.SelfContactPoint.Scheme; 
                        _log.Info("Locating service members. Using discovery [{0}], join decider [{1}], scheme [{2}]",
                            _discovery.GetType().Name,
                            _joinDecider.GetType().Name,
                            scheme
                        );
                        DiscoverContactPoints();
                        Become(Bootstrapping(Sender, scheme));
                        return true;
                    default:
                        return false;
                }
            };
        }

        protected virtual Receive Bootstrapping(IActorRef replyTo, string selfContactPointScheme)
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
                        OnContactPointsResolved(filteredContactPoints, selfContactPointScheme);
                        
                        ResetDiscoveryInterval(); // in case we were backed-off, we reset back to healthy intervals
                        StartSingleDiscoveryTimer(); // keep looking in case other nodes join the discovery
                        return true;

                    case Failure ex:
                        _log.Warning(ex.Exception, "Resolve attempt failed! Cause: {0}", ex.Exception);
                        _lastContactObservation = null;
                        BackoffDiscoveryInterval();
                        StartSingleDiscoveryTimer();
                        return true;

                    case Protocol.ObtainedSeedNodesObservation msg:
                    {
                        if (_lastContactObservation != null)
                        {
                            var observedAt = msg.ObservedAt;
                            var contactPoint = msg.ContactPoint;
                            var infoFromAddress = msg.SeedNodesSourceAddress;
                            var observedSeedNodes = msg.ObservedSeedNodes;
                            var contacts = _lastContactObservation;
                            if (contacts.ObservedContactPoints.Contains(contactPoint))
                            {
                                _log.Info("Contact point [{0}] returned [{1}] seed-nodes [{0}]",
                                    infoFromAddress,
                                    observedSeedNodes.Count,
                                    string.Join(", ", observedSeedNodes)
                                );

                                _seedNodesObservations = _seedNodesObservations.SetItem(
                                    contactPoint,
                                    new SeedNodesObservation(observedAt, contactPoint, infoFromAddress,
                                        observedSeedNodes));
                            }
                            
                            // if we got seed nodes it is likely that it should join those immediately
                            if(!observedSeedNodes.IsEmpty)
                                Decide();
                        }

                        return true;
                    }

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
                    
                    case Protocol.ProbingFailed f:
                    {
                        var contactPoint = f.ContactPoint;
                        if (_lastContactObservation != null)
                        {
                            var contacts = _lastContactObservation;
                            if (contacts.ObservedContactPoints.Contains(contactPoint))
                            {
                                _log.Info("Received signal that probing has failed, scheduling contact point probing again");
                                // child actor will have terminated now, so we ride on another discovery round to cause looking up
                                // target nodes and if the same still exists, that would cause probing it again
                                //
                                // we do this in order to not keep probing nodes which simply have been removed from the deployment
                            }
                        }
                        
                        // remove the previous observation since it might be obsolete
                        _seedNodesObservations = _seedNodesObservations.Remove(contactPoint);
                        StartSingleDiscoveryTimer();
                        return true;
                    }
                    
                    default:
                        return false;
                }
            };
        }

        private IEnumerable<string> FormatContactPoints(IEnumerable<ResolvedTarget> filteredContactPoints)
            => filteredContactPoints.Select(r => $"{r.Host}:{r.Port?.ToString() ?? "0"}");

        private void DiscoverContactPoints()
        {
            _log.Info("Looking up [{0}]", _lookup);
            _discovery.Lookup(_lookup, _settings.ContactPointDiscovery.ResolveTimeout).PipeTo(Self);
        }

        private void OnContactPointsResolved(
            IEnumerable<ResolvedTarget> contactPoints,
            string selfContactPointScheme)
        {
            var newObservation = new ServiceContactsObservation(
                DateTimeOffset.Now, 
                contactPoints.Where(t => t.Host != null || t.Address != null).ToImmutableHashSet());
            _lastContactObservation = _lastContactObservation != null
                ? _lastContactObservation.SameOrChanged(newObservation)
                : newObservation;
            
            // remove observations from contact points that are not included any more
            _seedNodesObservations = _seedNodesObservations
                .Where(kvp => newObservation.ObservedContactPoints.Contains(kvp.Key)).ToImmutableDictionary();

            foreach (var target in newObservation.ObservedContactPoints)
            {
                EnsureProbing(selfContactPointScheme, target);
            }
        }

        protected virtual IActorRef EnsureProbing(string selfContactPointScheme, ResolvedTarget contactPoint)
        {
            if (contactPoint.Address is null && contactPoint.Host is null)
            {
                _log.Warning("Contact point does not have address or host defined. Skipping.");
                return null;
            }
            
            var targetPort = contactPoint.Port ?? _settings.ContactPoint.FallbackPort;
            var rawBaseUri = $"{selfContactPointScheme}://{contactPoint.Address?.ToString() ?? contactPoint.Host}:{targetPort}";
            if (!string.IsNullOrEmpty(_settings.ManagementBasePath))
                rawBaseUri += $"/{_settings.ManagementBasePath}";
            var baseUri = new Uri(rawBaseUri);

            var childActorName = HttpContactPointBootstrap.Name(baseUri.Host, baseUri.Port);
            _log.Debug($"Ensuring probing actor: {childActorName}");
            
            // This should never really happen in well configured env, but it may happen that someone is confused with ports
            // and we end up trying to probe (using http for example) a port that actually is our own remoting port.
            // We actively bail out of this case and log a warning instead.
            var aboutToProbeSelfAddress =
                baseUri.Host == (_cluster.SelfAddress.Host ?? "---") &&
                baseUri.Port == (_cluster.SelfAddress.Port ?? -1);

            if (aboutToProbeSelfAddress)
            {
                _log.Warning(
                    "Misconfiguration detected! Attempted to start probing a contact-point which address [{0}] " +
                    "matches our local remoting address [{1}]. Avoiding probing this address. Consider double checking your service " +
                    "discovery and port configurations.",
                    baseUri,
                    _cluster.SelfAddress);
                return null;
            }

            var child = Context.Child(childActorName);
            if (!child.IsNobody())
                return child;
            
            var props = HttpContactPointBootstrap.Props(_settings, contactPoint, baseUri);
            return Context.ActorOf(props, childActorName);
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
            var currentTime = DateTimeOffset.Now;
            
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
                    return KeepProbing.Instance;
                }

                return task.Result;
            }, TaskContinuationOptions.ExecuteSynchronously).PipeTo(Self);
        }
    }
}