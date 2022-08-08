//-----------------------------------------------------------------------
// <copyright file="ClusterBootstrapSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Event;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Akka.Management.Cluster.Bootstrap.Tests
{
    public class ClusterBootstrapSettingsSpec
    {
        [Fact(DisplayName = "ClusterBootstrapSettings should have expected defaults")]
        public void HaveExpectedDefaults()
        {
            var config = ClusterBootstrap.DefaultConfiguration()
                .WithFallback(AkkaManagementProvider.DefaultConfiguration());
            
            var settings = ClusterBootstrapSettings.Create(config, NoLogger.Instance);
            settings.NewClusterEnabled.Should().BeTrue();
            
            settings.ContactPointDiscovery.ServiceName.Should().BeNull();
            settings.ContactPointDiscovery.PortName.Should().BeNull();
            settings.ContactPointDiscovery.Protocol.Should().Be("tcp");
            settings.ContactPointDiscovery.ServiceNamespace.Should().BeNull();
            settings.ContactPointDiscovery.DiscoveryMethod.Should().Be("akka.discovery");
            settings.ContactPointDiscovery.StableMargin.Should().Be(TimeSpan.FromSeconds(5));
            settings.ContactPointDiscovery.Interval.Should().Be(TimeSpan.FromSeconds(1));
            settings.ContactPointDiscovery.ExponentialBackoffRandomFactor.Should().Be(0.2);
            settings.ContactPointDiscovery.ExponentialBackoffMax.Should().Be(TimeSpan.FromSeconds(15));
            settings.ContactPointDiscovery.RequiredContactPointsNr.Should().Be(2);
            settings.ContactPointDiscovery.ResolveTimeout.Should().Be(TimeSpan.FromSeconds(3));
            settings.ContactPointDiscovery.ContactWithAllContactPoints.Should().BeTrue();

            settings.ContactPoint.FallbackPort.Should().Be(8558);
            settings.ContactPoint.FilterOnFallbackPort.Should().BeTrue();
            settings.ContactPoint.ProbingFailureTimeout.Should().Be(TimeSpan.FromSeconds(3));
            settings.ContactPoint.ProbeInterval.Should().Be(TimeSpan.FromSeconds(1));
            settings.ContactPoint.ProbeIntervalJitter.Should().Be(0.2);
            settings.JoinDecider.ImplClass.Should()
                .Be("Akka.Management.Cluster.Bootstrap.LowestAddressJoinDecider, Akka.Management.Cluster.Bootstrap");
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
                    StableMargin = 1.Seconds(),
                    Interval = 2.Seconds(),
                    ExponentialBackoffRandomFactor = 1.0,
                    ExponentialBackoffMax = 3.Seconds(),
                    RequiredContactPointsNr = 1,
                    ResolveTimeout = 4.Seconds(),
                    ContactWithAllContactPoints = false
                },
                ContactPoint = new ContactPointSetup
                {
                    FallbackPort = 1234,
                    FilterOnFallbackPort = false,
                    ProbingFailureTimeout = 1.Seconds(),
                    ProbeInterval = 2.Seconds(),
                    ProbeIntervalJitter = 1.0
                },
                JoinDecider = new JoinDeciderSetup
                {
                    Class = typeof(ClusterBootstrap)
                }
            };
            var settings = setup.Apply(original);
            settings.NewClusterEnabled.Should().BeFalse();
            
            settings.ContactPointDiscovery.ServiceName.Should().Be("a");
            settings.ContactPointDiscovery.PortName.Should().Be("b");
            settings.ContactPointDiscovery.Protocol.Should().Be("c");
            settings.ContactPointDiscovery.ServiceNamespace.Should().Be("d");
            settings.ContactPointDiscovery.DiscoveryMethod.Should().Be("e");
            settings.ContactPointDiscovery.StableMargin.Should().Be(1.Seconds());
            settings.ContactPointDiscovery.Interval.Should().Be(2.Seconds());
            settings.ContactPointDiscovery.ExponentialBackoffRandomFactor.Should().Be(1.0);
            settings.ContactPointDiscovery.ExponentialBackoffMax.Should().Be(3.Seconds());
            settings.ContactPointDiscovery.RequiredContactPointsNr.Should().Be(1);
            settings.ContactPointDiscovery.ResolveTimeout.Should().Be(4.Seconds());
            settings.ContactPointDiscovery.ContactWithAllContactPoints.Should().BeFalse();

            settings.ContactPoint.FallbackPort.Should().Be(1234);
            settings.ContactPoint.FilterOnFallbackPort.Should().BeFalse();
            settings.ContactPoint.ProbingFailureTimeout.Should().Be(1.Seconds());
            settings.ContactPoint.ProbeInterval.Should().Be(2.Seconds());
            settings.ContactPoint.ProbeIntervalJitter.Should().Be(1.0);
            
            settings.JoinDecider.ImplClass.Should()
                .Be(typeof(ClusterBootstrap).AssemblyQualifiedName);
        }
    }
}