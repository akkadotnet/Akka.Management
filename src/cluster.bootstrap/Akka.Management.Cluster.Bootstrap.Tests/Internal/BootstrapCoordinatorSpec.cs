//-----------------------------------------------------------------------
// <copyright file="ServerBuilder.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Event;
using Akka.Management.Cluster.Bootstrap.Internal;
using Akka.Management.Dsl;
using Akka.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Akka.Discovery.ServiceDiscovery;
using static Akka.Management.Cluster.Bootstrap.Internal.BootstrapCoordinator.Protocol;

namespace Akka.Management.Cluster.Bootstrap.Tests.Internal
{
    public class BootstrapCoordinatorSpec : TestKit.Xunit2.TestKit
    {
        private const string ServiceName = "bootstrap-coordinator-test-service";
        private static readonly Config Config = ConfigurationFactory.ParseString($@"
            akka.actor.provider = cluster
            akka.remote.dot-netty.tcp.port = 0
            akka.management.cluster.bootstrap {{
                contact-point-discovery.service-name = {ServiceName}
            }}")
            .WithFallback(ClusterBootstrap.DefaultConfiguration())
            .WithFallback(AkkaManagementProvider.DefaultConfiguration());

        private readonly Uri _selfUri;
        private readonly ClusterBootstrapSettings _settings;
        private readonly IJoinDecider _joinDecider;
        private readonly ServiceDiscovery _discovery; 

        public BootstrapCoordinatorSpec(ITestOutputHelper helper) : 
            base(Config, nameof(BootstrapCoordinatorSpec), helper)
        {
            _selfUri = new Uri("http://localhost:8558/");
            _settings = ClusterBootstrapSettings.Create(Sys.Settings.Config, Log);
            _joinDecider = new LowestAddressJoinDecider(Sys, _settings);
            _discovery = new MockDiscovery(Sys);
        }

        [Fact(DisplayName = "The bootstrap coordinator, after joining the cluster, should shut down")]
        public void BootStrapCoordinatorShouldShutDownAfterJoiningCluster()
        {
            MockDiscovery.Set(
                new Lookup(ServiceName, portName: null, protocol: "tcp"),
                () => Task.FromResult(new Resolved(
                    ServiceName,
                    new List<ResolvedTarget>
                    {
                        new ResolvedTarget("host1", null, null),
                        new ResolvedTarget("host1", null, null),
                        new ResolvedTarget("host2", null, null),
                        new ResolvedTarget("host2", null, null),
                    })));

            var targets = new AtomicReference<ImmutableList<ResolvedTarget>>(ImmutableList<ResolvedTarget>.Empty);
            var coordinator = Sys.ActorOf(Props.Create(() => new TestBootstrapCoordinator(
                _discovery, _joinDecider, _settings, targets)));
            Watch(coordinator);
            
            coordinator.Tell(new InitiateBootstrapping(_selfUri));
            AwaitAssert(() =>
            {
                var targetsToCheck = targets.Value;
                targetsToCheck.Count.Should().BeGreaterOrEqualTo(2);
                targetsToCheck.Select(t => t.Host).Should().Contain("host1");
                targetsToCheck.Select(t => t.Host).Should().Contain("host2");
                targetsToCheck.Where(t => t.Port != null).Select(t => t.Port).ToImmutableHashSet().Count.Should().Be(0);
            });
            
            coordinator.Tell(new JoinOtherSeedNodes(new [] {Akka.Cluster.Cluster.Get(Sys).SelfAddress}.ToImmutableHashSet()));
            ExpectTerminated(coordinator); // coordinator should stop itself after joining
        }

        [Fact(DisplayName = "The bootstrap coordinator, after joining self, should shut down")]
        public void BootStrapCoordinatorShouldShutDownAfterJoiningSelf()
        {
            MockDiscovery.Set(
                new Lookup(ServiceName, portName: null, protocol: "tcp"),
                () => Task.FromResult(new Resolved(
                    ServiceName,
                    new List<ResolvedTarget>
                    {
                        new ResolvedTarget("host1", null, null),
                        new ResolvedTarget("host1", null, null),
                        new ResolvedTarget("host2", null, null),
                        new ResolvedTarget("host2", null, null),
                    })));

            var targets = new AtomicReference<ImmutableList<ResolvedTarget>>(ImmutableList<ResolvedTarget>.Empty);
            var coordinator = Sys.ActorOf(Props.Create(() => new TestBootstrapCoordinator(
                _discovery, _joinDecider, _settings, targets)));
            Watch(coordinator);
            
            coordinator.Tell(new InitiateBootstrapping(_selfUri));
            AwaitAssert(() =>
            {
                var targetsToCheck = targets.Value;
                targetsToCheck.Count.Should().BeGreaterOrEqualTo(2);
                targetsToCheck.Select(t => t.Host).Should().Contain("host1");
                targetsToCheck.Select(t => t.Host).Should().Contain("host2");
                targetsToCheck.Where(t => t.Port != null).Select(t => t.Port).ToImmutableHashSet().Count.Should().Be(0);
            });
            
            coordinator.Tell(JoinSelf.Instance);
            ExpectTerminated(coordinator); // coordinator should stop itself after joining
        }
        
        [Fact(DisplayName = "The bootstrap coordinator, when avoiding named port lookups, should probe only on the Akka Management port")]
        public void BootStrapCoordinatorAvoidNamedPortLookupShouldProbeAkkaManagementPort()
        {
            MockDiscovery.Set(
                new Lookup(ServiceName, portName: null, protocol: "tcp"),
                () => Task.FromResult(new Resolved(
                    ServiceName,
                    new List<ResolvedTarget>
                    {
                        new ResolvedTarget("host1", 2552, null),
                        new ResolvedTarget("host1", 8558, null),
                        new ResolvedTarget("host2", 2552, null),
                        new ResolvedTarget("host2", 8558, null),
                    })));

            var targets = new AtomicReference<ImmutableList<ResolvedTarget>>(ImmutableList<ResolvedTarget>.Empty);
            var coordinator = Sys.ActorOf(Props.Create(() => new TestBootstrapCoordinator(
                    _discovery, _joinDecider, _settings, targets)));
            coordinator.Tell(new InitiateBootstrapping(_selfUri));
            AwaitAssert(() =>
            {
                var targetsToCheck = targets.Value;
                targetsToCheck.Count.Should().BeGreaterOrEqualTo(2);
                targetsToCheck.Select(t => t.Host).Should().Contain("host1");
                targetsToCheck.Select(t => t.Host).Should().Contain("host2");
                targetsToCheck.Select(t => t.Port).ToImmutableHashSet().Should().BeEquivalentTo(new HashSet<int> {8558});
            });
        }

        [Fact(DisplayName =
            "The bootstrap coordinator, when avoiding named port lookups, should probe all hosts with fallback port")]
        public void BootStrapCoordinatorAvoidNamedPortLookupShouldProbeAllHostWithFallbackPort()
        {
            MockDiscovery.Set(
                new Lookup(ServiceName, portName: null, protocol: "tcp"),
                () => Task.FromResult(new Resolved(
                    ServiceName,
                    new List<ResolvedTarget>
                    {
                        new ResolvedTarget("host1", null, null),
                        new ResolvedTarget("host1", null, null),
                        new ResolvedTarget("host2", null, null),
                        new ResolvedTarget("host2", null, null),
                    })));

            var targets = new AtomicReference<ImmutableList<ResolvedTarget>>(ImmutableList<ResolvedTarget>.Empty);
            var coordinator = Sys.ActorOf(Props.Create(() => new TestBootstrapCoordinator(
                _discovery, _joinDecider, _settings, targets)));
            coordinator.Tell(new InitiateBootstrapping(_selfUri));
            AwaitAssert(() =>
            {
                var targetsToCheck = targets.Value;
                targetsToCheck.Count.Should().BeGreaterOrEqualTo(2);
                targetsToCheck.Select(t => t.Host).Should().Contain("host1");
                targetsToCheck.Select(t => t.Host).Should().Contain("host2");
                targetsToCheck.Where(t => t.Port != null).Select(t => t.Port).ToImmutableHashSet().Count.Should().Be(0);
            });
        }
        
        [Fact(DisplayName =
            "BootstrapCoordinator target filtering should not filter when port-name is set")]
        public void BootStrapCoordinatorTargetFilteringShouldNotFilterWhenPortNameIsSet()
        {
            var beforeFiltering = new []
            {
                new ResolvedTarget("host1", 1, null),
                new ResolvedTarget("host1", 2, null),
                new ResolvedTarget("host2", 3, null),
                new ResolvedTarget("host2", 4, null),
            }.ToImmutableList();
            BootstrapCoordinator.SelectHosts(new Lookup("service", "cats"), 8558, false, beforeFiltering)
                .Should().BeEquivalentTo(beforeFiltering);
        }
        
        // For example when using DNS A-record-based discovery in K8s
        [Fact(DisplayName =
            "BootstrapCoordinator target filtering should filter when port-name is not set")]
        public void BootStrapCoordinatorTargetFilteringShouldFilterWhenPortNameIsNotSet()
        {
            var beforeFiltering = new []
            {
                new ResolvedTarget("host1", 8558, null),
                new ResolvedTarget("host1", 2, null),
                new ResolvedTarget("host2", 8558, null),
                new ResolvedTarget("host2", 4, null),
            }.ToImmutableList();
            BootstrapCoordinator.SelectHosts(new Lookup("service"), 8558, true, beforeFiltering)
                .Should().BeEquivalentTo(new []
                {
                    new ResolvedTarget("host1", 8558, null),
                    new ResolvedTarget("host2", 8558, null),
                }.ToImmutableList());
        }
        
        // For example when using ECS service discovery
        [Fact(DisplayName =
            "BootstrapCoordinator target filtering should not filter when port-name is not set but filtering disabled")]
        public void BootStrapCoordinatorTargetFilteringShouldNotFilterWhenPortNameIsNotSetButFilteringDisabled()
        {
            var beforeFiltering = new []
            {
                new ResolvedTarget("host1", 8558, null),
                new ResolvedTarget("host1", 2, null),
                new ResolvedTarget("host2", 8558, null),
                new ResolvedTarget("host2", 4, null),
            }.ToImmutableList();
            BootstrapCoordinator.SelectHosts(new Lookup("service"), 8558, false, beforeFiltering)
                .Should().BeEquivalentTo(beforeFiltering);
        }
        
        [Fact(DisplayName =
            "BootstrapCoordinator target filtering should not filter if there is a single target per host")]
        public void BootStrapCoordinatorTargetFilteringShouldNotFilterIfThereIsASingleTargetPerHost()
        {
            var beforeFiltering = new []
            {
                new ResolvedTarget("host1", 8558, null),
                new ResolvedTarget("host2", 2, null),
                new ResolvedTarget("host3", 8558, null),
                new ResolvedTarget("host4", 4, null),
            }.ToImmutableList();
            BootstrapCoordinator.SelectHosts(new Lookup("service"), 8558, true, beforeFiltering)
                .Should().BeEquivalentTo(beforeFiltering);
        }
        
        private class TestBootstrapCoordinator : BootstrapCoordinator
        {
            private readonly ILoggingAdapter _log;
            private readonly AtomicReference<ImmutableList<ResolvedTarget>> _targets;
            
            public TestBootstrapCoordinator(
                ServiceDiscovery discovery,
                IJoinDecider joinDecider,
                ClusterBootstrapSettings settings,
                AtomicReference<ImmutableList<ResolvedTarget>> targets) : base(discovery, joinDecider, settings)
            {
                _log = Context.GetLogger();
                _targets = targets;
            }

            protected override IActorRef EnsureProbing(string selfContactPointScheme, ResolvedTarget contactPoint)
            {
                _log.Debug($"Resolving {contactPoint}");
                var targetSoFar = _targets.Value;
                _targets.CompareAndSet(targetSoFar, targetSoFar.Add(contactPoint));
                return ActorRefs.Nobody;
            }
        }
    }
}