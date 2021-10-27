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
using Xunit;

namespace Akka.Management.Cluster.Bootstrap.Tests
{
    public class ClusterBootstrapSettingsSpec : TestKit.Xunit2.TestKit
    {
        private readonly Config _config = Config.Empty
            .WithFallback(ClusterBootstrap.DefaultConfiguration())
            .WithFallback(AkkaManagementProvider.DefaultConfiguration());
        
        [Fact(DisplayName = "ClusterBootstrapSettings should have expected defaults")]
        public void HaveExpectedDefaults()
        {
            var settings = new ClusterBootstrapSettings(_config, NoLogger.Instance);
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
    }
}