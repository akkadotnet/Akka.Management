// -----------------------------------------------------------------------
//  <copyright file="ClusterBootstrapSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor.Setup;

namespace Akka.Management.Cluster.Bootstrap
{
    public sealed class ClusterBootstrapSetup: Setup
    {
        public bool? NewClusterEnabled { get; set; }
        public ContactPointDiscoverySetup ContactPointDiscovery { get; set; }
        public ContactPointSetup ContactPoint { get; set; }
        public JoinDeciderSetup JoinDecider { get; set; }

        internal ClusterBootstrapSettings Apply(ClusterBootstrapSettings settings)
            => settings.Copy(
                newClusterEnabled: NewClusterEnabled,
                contactPointDiscovery: ContactPointDiscovery?.Apply(settings.ContactPointDiscovery),
                contactPoint: ContactPoint?.Apply(settings.ContactPoint),
                joinDecider: JoinDecider?.Apply(settings.JoinDecider));
    }

    public sealed class ContactPointDiscoverySetup: Setup
    {
        public string ServiceName { get; set; }
        public string ServiceNamespace { get; set; }
        public string PortName { get; set; }
        public string Protocol { get; set; }
        public string DiscoveryMethod { get; set; }
        public string EffectiveName { get; set; }
        public TimeSpan? StableMargin { get; set; }
        public TimeSpan? Interval { get; set; }
        public double? ExponentialBackoffRandomFactor { get; set; }
        public TimeSpan? ExponentialBackoffMax { get; set; }
        public int? RequiredContactPointsNr { get; set; }
        public bool? ContactWithAllContactPoints { get; set; }
        public TimeSpan? ResolveTimeout { get; set; }

        internal ClusterBootstrapSettings.ContactPointDiscoverySettings Apply(
            ClusterBootstrapSettings.ContactPointDiscoverySettings settings)
            => settings.Copy(
                serviceName: ServiceName,
                serviceNamespace: ServiceNamespace,
                portName: PortName,
                protocol: Protocol,
                effectiveName: EffectiveName,
                discoveryMethod: DiscoveryMethod,
                stableMargin: StableMargin,
                interval: Interval,
                exponentialBackoffRandomFactor: ExponentialBackoffRandomFactor,
                exponentialBackoffMax: ExponentialBackoffMax,
                requiredContactPointsNr: RequiredContactPointsNr,
                contactWithAllContactPoints: ContactWithAllContactPoints,
                resolveTimeout: ResolveTimeout);
    }

    public sealed class ContactPointSetup : Setup
    {
        public int? FallbackPort { get; set; }
        public bool? FilterOnFallbackPort { get; set; }
        public TimeSpan? ProbingFailureTimeout { get; set; }
        public TimeSpan? ProbeInterval { get; set; }
        public double? ProbeIntervalJitter { get; set; }

        internal ClusterBootstrapSettings.ContactPointSettings Apply(ClusterBootstrapSettings.ContactPointSettings settings)
            => settings.Copy(
                fallbackPort: FallbackPort, 
                filterOnFallbackPort: FilterOnFallbackPort,
                probingFailureTimeout: ProbingFailureTimeout,
                probeInterval: ProbeInterval,
                probeIntervalJitter: ProbeIntervalJitter);
    }
    
    public sealed class JoinDeciderSetup : Setup
    {
        public Type Class { get; set; }

        internal ClusterBootstrapSettings.JoinDeciderSettings Apply(ClusterBootstrapSettings.JoinDeciderSettings settings)
            => Class == null ? settings : settings.WithImplClass(Class.AssemblyQualifiedName);
    }
}