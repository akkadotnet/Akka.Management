//-----------------------------------------------------------------------
// <copyright file="ClusterBootstrapSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Annotations;
using Akka.Configuration;
using Akka.Event;

namespace Akka.Management.Cluster.Bootstrap
{
    [InternalApi]
    public sealed class ClusterBootstrapSettings
    {
        public sealed class ContactPointDiscoverySettings
        {
            public static ContactPointDiscoverySettings Create(Config config)
            {
                var discoveryConfig = config.GetConfig("akka.management.cluster.bootstrap.contact-point-discovery");
                return new ContactPointDiscoverySettings(
                    discoveryConfig.GetString("service-name"),
                    discoveryConfig.GetString("service-namespace"),
                    discoveryConfig.GetString("port-name"),
                    discoveryConfig.GetString("protocol"),
                    discoveryConfig.GetString("effective-name"),
                    discoveryConfig.GetString("discovery-method"),
                    discoveryConfig.GetTimeSpan("stable-margin", null, false),
                    discoveryConfig.GetTimeSpan("interval", null, false),
                    discoveryConfig.GetDouble("exponential-backoff-random-factor"),
                    discoveryConfig.GetTimeSpan("exponential-backoff-max", null, false),
                    discoveryConfig.GetInt("required-contact-point-nr"),
                    discoveryConfig.GetBoolean("contact-with-all-contact-points"),
                    discoveryConfig.GetTimeSpan("resolve-timeout", null, false));
            }
            
            private readonly string? _effectiveName;

            private ContactPointDiscoverySettings(
                string? serviceName,
                string? serviceNamespace,
                string? portName,
                string? protocol,
                string? effectiveName,
                string discoveryMethod,
                TimeSpan stableMargin,
                TimeSpan interval,
                double exponentialBackoffRandomFactor,
                TimeSpan exponentialBackoffMax,
                int requiredContactPointsNr,
                bool contactWithAllContactPoints,
                TimeSpan resolveTimeout)
            {
                ServiceName = serviceName;
                if(string.IsNullOrEmpty(ServiceName) || ServiceName == "<service-name>")
                    ServiceName = Environment.GetEnvironmentVariable("AKKA__CLUSTER__BOOTSTRAP__SERVICE_NAME");
                
                ServiceNamespace = serviceNamespace;
                if (string.IsNullOrEmpty(ServiceNamespace) || ServiceNamespace == "<service-namespace>")
                    ServiceNamespace = null;
                
                PortName = portName;
                if (string.IsNullOrEmpty(PortName))
                    PortName = null;
                
                Protocol = protocol;
                if (string.IsNullOrEmpty(Protocol))
                    Protocol = null;

                _effectiveName = effectiveName;
                if (string.IsNullOrEmpty(Protocol) || _effectiveName == "<effective-name>")
                    _effectiveName = null;
                
                DiscoveryMethod = discoveryMethod;
                StableMargin = stableMargin;
                Interval = interval;
                ExponentialBackoffRandomFactor = exponentialBackoffRandomFactor;
                
                ExponentialBackoffMax = exponentialBackoffMax;
                if (ExponentialBackoffMax < Interval)
                    throw new ConfigurationException("exponential-backoff-max has to be greater or equal to interval");
                
                RequiredContactPointsNr = requiredContactPointsNr;
                ContactWithAllContactPoints = contactWithAllContactPoints;
                ResolveTimeout = resolveTimeout;
            }
            
            public string? ServiceName { get; }
            public string? ServiceNamespace { get; }
            public string? PortName { get; }
            public string? Protocol { get; }

            public string? EffectiveName(ActorSystem system)
            {
                if (!string.IsNullOrEmpty(_effectiveName))
                    return _effectiveName;

                var service = string.IsNullOrEmpty(ServiceName)
                    ? system.Name.ToLowerInvariant().Replace(" ", "-").Replace("_", "-")
                    : ServiceName;
                service = string.IsNullOrEmpty(ServiceNamespace) ? service : $"{service}.{ServiceNamespace}";
                return service;
            }
            
            public string DiscoveryMethod { get; }
            public TimeSpan StableMargin { get; }
            public TimeSpan Interval { get; }
            public double ExponentialBackoffRandomFactor { get; }
            public TimeSpan ExponentialBackoffMax { get; }
            public int RequiredContactPointsNr { get; }
            public bool ContactWithAllContactPoints { get; }
            public TimeSpan ResolveTimeout { get; }

            internal ContactPointDiscoverySettings Copy(
                string? serviceName = null,
                string? serviceNamespace = null,
                string? portName = null,
                string? protocol = null,
                string? effectiveName = null,
                string? discoveryMethod = null,
                TimeSpan? stableMargin = null,
                TimeSpan? interval = null,
                double? exponentialBackoffRandomFactor = null,
                TimeSpan? exponentialBackoffMax = null,
                int? requiredContactPointsNr = null,
                bool? contactWithAllContactPoints = null,
                TimeSpan? resolveTimeout = null)
                => new (
                    serviceName: serviceName ?? ServiceName,
                    serviceNamespace: serviceNamespace ?? ServiceNamespace,
                    portName: portName ?? PortName,
                    protocol: protocol ?? Protocol,
                    effectiveName: effectiveName ?? _effectiveName,
                    discoveryMethod: discoveryMethod ?? DiscoveryMethod,
                    stableMargin: stableMargin ?? StableMargin,
                    interval: interval ?? Interval,
                    exponentialBackoffRandomFactor: exponentialBackoffRandomFactor ?? ExponentialBackoffRandomFactor,
                    exponentialBackoffMax: exponentialBackoffMax ?? ExponentialBackoffMax,
                    requiredContactPointsNr: requiredContactPointsNr ?? RequiredContactPointsNr,
                    contactWithAllContactPoints: contactWithAllContactPoints ?? ContactWithAllContactPoints,
                    resolveTimeout: resolveTimeout ?? ResolveTimeout);
        }
        
        public sealed class ContactPointSettings
        {
            public static ContactPointSettings Create(Config config)
            {
                var contactPointConfig = config.GetConfig("akka.management.cluster.bootstrap.contact-point");
                var fallback = contactPointConfig.GetString("fallback-port");
                var fallbackPort = string.IsNullOrWhiteSpace(fallback) || fallback == "<fallback-port>"
                    ? (int?) null : int.Parse(fallback);
                
                var probeInterval = contactPointConfig.GetTimeSpan("probe-interval", TimeSpan.FromSeconds(5), false);
                var probeFailureTimeout = contactPointConfig.GetTimeSpan("probing-failure-timeout", TimeSpan.FromSeconds(3), false);
                
                var staleEntryTimeoutStr = contactPointConfig.GetString("stale-contact-point-timeout");
                var staleEntryTimeout = string.IsNullOrWhiteSpace(staleEntryTimeoutStr)
                                        || staleEntryTimeoutStr is "off"
                                        || staleEntryTimeoutStr is "false"
                                        || staleEntryTimeoutStr is "no"
                        ? (TimeSpan?) null 
                        : contactPointConfig.GetTimeSpan("stale-contact-point-timeout");
                
                return new ContactPointSettings(
                    fallbackPort: fallbackPort,
                    filterOnFallbackPort: contactPointConfig.GetBoolean("filter-on-fallback-port"),
                    probingFailureTimeout: probeFailureTimeout,
                    probeInterval: probeInterval,
                    probeIntervalJitter: contactPointConfig.GetDouble("probe-interval-jitter"),
                    staleContactPointTimeout: staleEntryTimeout);
            }
            
            private ContactPointSettings(
                int? fallbackPort,
                bool filterOnFallbackPort,
                TimeSpan probingFailureTimeout,
                TimeSpan probeInterval,
                double probeIntervalJitter,
                TimeSpan? staleContactPointTimeout)
            {
                FallbackPort = fallbackPort;
                FilterOnFallbackPort = filterOnFallbackPort;
                ProbingFailureTimeout = probingFailureTimeout;
                ProbeInterval = probeInterval;
                ProbeIntervalJitter = probeIntervalJitter;
                StaleContactPointTimeout = staleContactPointTimeout;
            }
            
            public int? FallbackPort { get; }
            public bool FilterOnFallbackPort { get; }
            public TimeSpan ProbingFailureTimeout { get; }
            public TimeSpan ProbeInterval { get; }
            public double ProbeIntervalJitter { get; }
            public int MaxSeedNodesToExpose { get; } = 5;
            public TimeSpan? StaleContactPointTimeout { get; }

            internal ContactPointSettings Copy(
                int? fallbackPort,
                bool? filterOnFallbackPort,
                TimeSpan? probingFailureTimeout,
                TimeSpan? probeInterval,
                double? probeIntervalJitter,
                TimeSpan? staleContactPointTimeout)
                => new ContactPointSettings(
                    fallbackPort: fallbackPort ?? FallbackPort,
                    filterOnFallbackPort: filterOnFallbackPort ?? FilterOnFallbackPort,
                    probingFailureTimeout: probingFailureTimeout ?? ProbingFailureTimeout,
                    probeInterval: probeInterval ?? ProbeInterval,
                    probeIntervalJitter: probeIntervalJitter ?? ProbeIntervalJitter,
                    staleContactPointTimeout: staleContactPointTimeout ?? StaleContactPointTimeout);
        }
        
        public sealed class JoinDeciderSettings
        {
            public static JoinDeciderSettings Create(Config config)
                => new JoinDeciderSettings(config.GetString("akka.management.cluster.bootstrap.join-decider.class"));
            
            public string ImplClass { get; }

            public JoinDeciderSettings WithImplClass(string implClass)
                => new JoinDeciderSettings(implClass);
            
            private JoinDeciderSettings(string implClass)
            {
                ImplClass = implClass;
            }
        }
        
        public static ClusterBootstrapSettings Create(Config config, ILoggingAdapter log)
            => new ClusterBootstrapSettings(
                managementBasePath: config.GetString("akka.management.http.base-path"),
                newClusterEnabled: config.GetBoolean("akka.management.cluster.bootstrap.new-cluster-enabled"),
                contactPointDiscovery: ContactPointDiscoverySettings.Create(config),
                contactPoint: ContactPointSettings.Create(config),
                joinDecider: JoinDeciderSettings.Create(config),
                log: log);

        private readonly ILoggingAdapter _log;

        private ClusterBootstrapSettings(
            string? managementBasePath,
            bool newClusterEnabled,
            ContactPointDiscoverySettings contactPointDiscovery,
            ContactPointSettings contactPoint,
            JoinDeciderSettings joinDecider,
            ILoggingAdapter log)
        {
            ManagementBasePath = managementBasePath;
            if (string.IsNullOrWhiteSpace(ManagementBasePath))
                ManagementBasePath = null;
            
            NewClusterEnabled = newClusterEnabled;
            ContactPointDiscovery = contactPointDiscovery;
            ContactPoint = contactPoint;
            JoinDecider = joinDecider;
            _log = log;
        }
        
        public string? ManagementBasePath { get; }
        public bool NewClusterEnabled { get; }
        public ContactPointDiscoverySettings ContactPointDiscovery { get; }
        public ContactPointSettings ContactPoint { get; }
        public JoinDeciderSettings JoinDecider { get; }

        internal ClusterBootstrapSettings Copy(
            bool? newClusterEnabled = null,
            ContactPointDiscoverySettings? contactPointDiscovery = null,
            ContactPointSettings? contactPoint = null,
            JoinDeciderSettings? joinDecider = null)
            => new (
                managementBasePath: ManagementBasePath,
                newClusterEnabled: newClusterEnabled ?? NewClusterEnabled,
                contactPointDiscovery: contactPointDiscovery ?? ContactPointDiscovery,
                contactPoint: contactPoint ?? ContactPoint,
                joinDecider: joinDecider ?? JoinDecider,
                log: _log
            );
    }
}