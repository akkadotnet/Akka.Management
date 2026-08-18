//-----------------------------------------------------------------------
// <copyright file="ClusterBootstrapSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Event;
using Akka.Management.Cluster.Bootstrap;
using Akka.Management.Dsl;
using Xunit;

namespace Akka.Management.Tests.Cluster.Bootstrap
{
    public class ClusterBootstrapSettingsSpec
    {
        [Fact(DisplayName = "ClusterBootstrapSettings should have expected defaults")]
        public void HaveExpectedDefaults()
        {
            var config = ClusterBootstrap.DefaultConfiguration()
                .WithFallback(AkkaManagementProvider.DefaultConfiguration());
            
            var settings = ClusterBootstrapSettings.Create(config, NoLogger.Instance);
            Assert.True(settings.NewClusterEnabled);

            Assert.Null(settings.ContactPointDiscovery.ServiceName);
            Assert.Null(settings.ContactPointDiscovery.PortName);
            Assert.Equal("tcp", settings.ContactPointDiscovery.Protocol);
            Assert.Null(settings.ContactPointDiscovery.ServiceNamespace);
            Assert.Equal("akka.discovery", settings.ContactPointDiscovery.DiscoveryMethod);
            Assert.Equal(TimeSpan.FromSeconds(5), settings.ContactPointDiscovery.StableMargin);
            Assert.Equal(TimeSpan.FromSeconds(1), settings.ContactPointDiscovery.Interval);
            Assert.Equal(0.2, settings.ContactPointDiscovery.ExponentialBackoffRandomFactor);
            Assert.Equal(TimeSpan.FromSeconds(15), settings.ContactPointDiscovery.ExponentialBackoffMax);
            Assert.Equal(2, settings.ContactPointDiscovery.RequiredContactPointsNr);
            Assert.Equal(TimeSpan.FromSeconds(3), settings.ContactPointDiscovery.ResolveTimeout);
            Assert.True(settings.ContactPointDiscovery.ContactWithAllContactPoints);

            Assert.Null(settings.ContactPoint.FallbackPort);
            Assert.True(settings.ContactPoint.FilterOnFallbackPort);
            Assert.Equal(TimeSpan.FromSeconds(3), settings.ContactPoint.ProbingFailureTimeout);
            Assert.Equal(TimeSpan.FromSeconds(5), settings.ContactPoint.ProbeInterval);
            Assert.Equal(0.2, settings.ContactPoint.ProbeIntervalJitter);
            Assert.Equal("Akka.Management.Cluster.Bootstrap.LowestAddressJoinDecider, Akka.Management",
                settings.JoinDecider.ImplClass);
        }
        
        [Fact(DisplayName = "ClusterBootstrapSetup should override ClusterBootstrapSettings")]
        public void SetupOverridesSettings()
        {
            var config = ClusterBootstrap.DefaultConfiguration()
                .WithFallback(AkkaManagementProvider.DefaultConfiguration());
            
            var original = ClusterBootstrapSettings.Create(config, NoLogger.Instance);
            var setup = new ClusterBootstrapSetup
            {
                NewClusterEnabled = false,
                ContactPointDiscovery = new ContactPointDiscoverySetup
                {
                    ServiceName = "a",
                    PortName = "b",
                    Protocol = "c",
                    ServiceNamespace = "d",
                    DiscoveryMethod = "e",
                    StableMargin = TimeSpan.FromSeconds(1),
                    Interval = TimeSpan.FromSeconds(2),
                    ExponentialBackoffRandomFactor = 1.0,
                    ExponentialBackoffMax = TimeSpan.FromSeconds(3),
                    RequiredContactPointsNr = 1,
                    ResolveTimeout = TimeSpan.FromSeconds(4),
                    ContactWithAllContactPoints = false
                },
                ContactPoint = new ContactPointSetup
                {
                    FallbackPort = 1234,
                    FilterOnFallbackPort = false,
                    ProbeInterval = TimeSpan.FromSeconds(2),
                    ProbingFailureTimeout = TimeSpan.FromSeconds(4),
                    ProbeIntervalJitter = 1.0
                },
                JoinDecider = new JoinDeciderSetup
                {
                    Class = typeof(ClusterBootstrap)
                }
            };
            var settings = setup.Apply(original);
            Assert.False(settings.NewClusterEnabled);

            Assert.Equal("a", settings.ContactPointDiscovery.ServiceName);
            Assert.Equal("b", settings.ContactPointDiscovery.PortName);
            Assert.Equal("c", settings.ContactPointDiscovery.Protocol);
            Assert.Equal("d", settings.ContactPointDiscovery.ServiceNamespace);
            Assert.Equal("e", settings.ContactPointDiscovery.DiscoveryMethod);
            Assert.Equal(TimeSpan.FromSeconds(1), settings.ContactPointDiscovery.StableMargin);
            Assert.Equal(TimeSpan.FromSeconds(2), settings.ContactPointDiscovery.Interval);
            Assert.Equal(1.0, settings.ContactPointDiscovery.ExponentialBackoffRandomFactor);
            Assert.Equal(TimeSpan.FromSeconds(3), settings.ContactPointDiscovery.ExponentialBackoffMax);
            Assert.Equal(1, settings.ContactPointDiscovery.RequiredContactPointsNr);
            Assert.Equal(TimeSpan.FromSeconds(4), settings.ContactPointDiscovery.ResolveTimeout);
            Assert.False(settings.ContactPointDiscovery.ContactWithAllContactPoints);

            Assert.Equal(1234, settings.ContactPoint.FallbackPort);
            Assert.False(settings.ContactPoint.FilterOnFallbackPort);
            Assert.Equal(TimeSpan.FromSeconds(2), settings.ContactPoint.ProbeInterval);
            Assert.Equal(TimeSpan.FromSeconds(4), settings.ContactPoint.ProbingFailureTimeout);
            Assert.Equal(1.0, settings.ContactPoint.ProbeIntervalJitter);

            Assert.Equal(typeof(ClusterBootstrap).AssemblyQualifiedName,
                settings.JoinDecider.ImplClass);
        }
    }
}