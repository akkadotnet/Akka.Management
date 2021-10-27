//-----------------------------------------------------------------------
// <copyright file="ClusterBootstrapSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;

namespace Akka.Management.Cluster.Bootstrap
{
    public sealed class ClusterBootstrapSettings
    {
        public sealed class ContactPointDiscoverySettings
        {
            private readonly string _effectiveName;

            public ContactPointDiscoverySettings(Config bootstrapConfig)
            {
                var discoveryConfig = bootstrapConfig.GetConfig("contact-point-discovery");

                ServiceName = discoveryConfig.GetString("service-name");
                if(string.IsNullOrEmpty(ServiceName) || ServiceName == "<service-name>")
                    ServiceName = Environment.GetEnvironmentVariable("AKKA__CLUSTER__BOOTSTRAP__SERVICE_NAME");

                ServiceNamespace = discoveryConfig.GetString("service-namespace");
                if (string.IsNullOrEmpty(ServiceNamespace) || ServiceNamespace == "<service-namespace>")
                    ServiceNamespace = null;
                
                PortName = discoveryConfig.GetString("port-name");
                if (string.IsNullOrEmpty(PortName))
                    PortName = null;
                
                Protocol = discoveryConfig.GetString("protocol");
                if (string.IsNullOrEmpty(Protocol))
                    Protocol = null;
                
                _effectiveName = discoveryConfig.GetString("effective-name");
                if (string.IsNullOrEmpty(Protocol) || _effectiveName == "<effective-name>")
                    _effectiveName = null;
                
                DiscoveryMethod = discoveryConfig.GetString("discovery-method");
                StableMargin = discoveryConfig.GetTimeSpan("stable-margin", null, false);
                Interval = discoveryConfig.GetTimeSpan("interval", null, false);
                ExponentialBackoffRandomFactor = discoveryConfig.GetDouble("exponential-backoff-random-factor");
                ExponentialBackoffMax = discoveryConfig.GetTimeSpan("exponential-backoff-max", null, false);

                if (ExponentialBackoffMax < Interval)
                    throw new ConfigurationException("exponential-backoff-max has to be greater or equal to interval");

                RequiredContactPointsNr = discoveryConfig.GetInt("required-contact-point-nr");
                ContactWithAllContactPoints = discoveryConfig.GetBoolean("contact-with-all-contact-points");
                ResolveTimeout = discoveryConfig.GetTimeSpan("resolve-timeout", null, false);
            }
            
            public string ServiceName { get; }
            public string ServiceNamespace { get; }
            public string PortName { get; }
            public string Protocol { get; }

            public string EffectiveName(ActorSystem system)
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
        }
        
        public sealed class ContactPointSettings
        {
            public ContactPointSettings(Config bootConfig, Config config)
            {
                var contactPointConfig = bootConfig.GetConfig("contact-point");

                var fallback = contactPointConfig.GetString("fallback-port");
                FallbackPort = string.IsNullOrWhiteSpace(fallback) || fallback == "<fallback-port>"
                    ? config.GetInt("akka.management.http.port")
                    : int.Parse(fallback);
                
                FilterOnFallbackPort = contactPointConfig.GetBoolean("filter-on-fallback-port");
                ProbingFailureTimeout = contactPointConfig.GetTimeSpan("probing-failure-timeout", null, false);
                ProbeInterval = contactPointConfig.GetTimeSpan("probe-interval", null, false);
                ProbeIntervalJitter = contactPointConfig.GetDouble("probe-interval-jitter");
            }
            
            public int FallbackPort { get; }
            public bool FilterOnFallbackPort { get; }
            public TimeSpan ProbingFailureTimeout { get; }
            public TimeSpan ProbeInterval { get; }
            public double ProbeIntervalJitter { get; }
            public int MaxSeedNodesToExpose { get; } = 5;
        }
        
        public sealed class JoinDeciderSettings
        {
            public string ImplClass { get; }

            public JoinDeciderSettings(Config bootConfig)
            {
                ImplClass = bootConfig.GetString("join-decider.class");
            }
        }
        
        private readonly ILoggingAdapter _log;
            
        public ClusterBootstrapSettings(Config config, ILoggingAdapter log)
        {
            _log = log;

            ManagementBasePath = config.GetString("akka.management.http.base-path");
            if (string.IsNullOrWhiteSpace(ManagementBasePath))
                ManagementBasePath = null;

            var bootConfig = config.GetConfig("akka.management.cluster.bootstrap");
            NewClusterEnabled = bootConfig.GetBoolean("new-cluster-enabled");
            ContactPointDiscovery = new ContactPointDiscoverySettings(bootConfig);
            ContactPoint = new ContactPointSettings(bootConfig, config);
            JoinDecider = new JoinDeciderSettings(bootConfig);
        }

        public string ManagementBasePath { get; }
        public bool NewClusterEnabled { get; }
        public ContactPointDiscoverySettings ContactPointDiscovery { get; }
        public ContactPointSettings ContactPoint { get; }
        public JoinDeciderSettings JoinDecider { get; }
    }
}