//-----------------------------------------------------------------------
// <copyright file="JoinDeciderSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Management.Cluster.Bootstrap.Util;
using Akka.Management.Dsl;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Akka.Discovery.ServiceDiscovery;

namespace Akka.Management.Cluster.Bootstrap.Tests
{
    public abstract class JoinDeciderSpec: IAsyncLifetime
    {
        protected virtual int ManagementPort { get; } = SocketUtil.TemporaryTcpAddress("127.0.0.1").Port;
        protected virtual int RemotingPort { get; } = SocketUtil.TemporaryTcpAddress("127.0.0.1").Port;

        protected Config Config => ConfigurationFactory.ParseString($@"
            akka {{
                loglevel = INFO

                cluster.http.management.port = {ManagementPort}
                remote.dot-netty.tcp.port = {RemotingPort}

                discovery {{
                    mock-dns.class = ""akka.discovery.MockDiscovery""
                }}

                management {{
                    cluster.bootstrap {{
                        contact-point-discovery {{
                            discovery-method = mock-dns
                            service-namespace = ""svc.cluster.local""
                            required-contact-point-nr = 3
                        }}
                    }}

                    http {{
                        hostname = ""10.0.0.2""
                        base-path = ""test""
                        port = {ManagementPort}
                    }}
                }}
            }}")
            .WithFallback(AkkaManagementProvider.DefaultConfiguration())
            .WithFallback(ClusterBootstrap.DefaultConfiguration());

        protected readonly ResolvedTarget ContactA = new (
            host: "10-0-0-2.default.pod.cluster.local",
            port: null,
            address: IPAddress.Parse("10.0.0.2")); 
        
        protected readonly ResolvedTarget ContactB = new (
            host: "10-0-0-3.default.pod.cluster.local",
            port: null,
            address: IPAddress.Parse("10.0.0.3")); 
        
        protected readonly ResolvedTarget ContactC = new (
            host: "10-0-0-4.default.pod.cluster.local",
            port: null,
            address: IPAddress.Parse("10.0.0.4"));

        protected ActorSystem? System { get; private set; }
        protected ILoggingAdapter? Log { get; private set; }

        private readonly ITestOutputHelper _output;
        private readonly string _systemName;
        private readonly Config _config;

        protected JoinDeciderSpec(Config? config, string systemName, ITestOutputHelper output)
        {
            _systemName = systemName;
            _output = output;
            _config = config?.WithFallback(Config) ?? Config;
        }
        
        public Task InitializeAsync()
        {
            System = ActorSystem.Create(_systemName, _config);
            var extSystem = (ExtendedActorSystem)System;
            var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Tell(new InitializeLogger(System.EventStream));
            Log = Logging.GetLogger(System, GetType());
            Start();
            
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if(System is { })
                await System.Terminate();
        }

        protected abstract Task Start();
        
        protected void AssertList<T>(List<T> expected, List<T> actual)
        {
            if (expected.Count != actual.Count)
                throw new Exception("List does not match");

            for (var i = 0; i < expected.Count; i++)
            {
                expected[i].Should().BeEquivalentTo(actual[i]);
            }
        }
    }

    public class LowestAddressJoinDeciderSpec : JoinDeciderSpec
    {
        private ClusterBootstrapSettings? _settings;

        public LowestAddressJoinDeciderSpec(ITestOutputHelper output) : base(null, "join-decider-spec-system", output)
        {
        }

        protected override Task Start()
        {
            _settings = ClusterBootstrapSettings.Create(System!.Settings.Config, Log!);
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "LowestAddressJoinDecider should sort ResolvedTarget by lowest hostname:port")]
        public void SortResolvedTargetByLowestHostnameAndPort()
        {
            // ReSharper disable RedundantArgumentDefaultValue
            var list = new List<ResolvedTarget>
            {
                new("c", null, null),
                new("a", null, null),
                new("b", null, null),
            };
            list.Sort(ResolvedTargetComparer.Instance);
            AssertList(list, new List<ResolvedTarget>
            {
                new("a", null, null),
                new("b", null, null),
                new("c", null, null),
            });
            
            list = new List<ResolvedTarget>
            {
                new("c", 1, null),
                new("a", 3, null),
                new("b", 2, null),
            };
            list.Sort(ResolvedTargetComparer.Instance);
            AssertList(list, new List<ResolvedTarget>
            {
                new("a", 3, null),
                new("b", 2, null),
                new("c", 1, null),
            });
            
            list = new List<ResolvedTarget>
            {
                new("a", 2, null),
                new("a", 1, null),
                new("a", 3, null),
            };
            list.Sort(ResolvedTargetComparer.Instance);
            AssertList(list, new List<ResolvedTarget>
            {
                new("a", 1, null),
                new("a", 2, null),
                new("a", 3, null),
            });
            // ReSharper restore RedundantArgumentDefaultValue
        }

        [Fact(DisplayName = "LowestAddressJoinDecider, when addresses are known, should sort deterministically on address even when names are inconsistent")]
        public void SortDeterministicallyOnAddressEvenWhenNamesAreInconsistent()
        {
            var addr1 = IPAddress.Parse("127.0.0.1");
            var addr2 = IPAddress.Parse("127.0.0.2");
            var addr3 = IPAddress.Parse("127.0.0.3");
            
            var list = new List<ResolvedTarget>
            {
                new("c", null, addr2),
                new("x", null, addr1),
                new("b", null, addr3),
            };
            list.Sort(ResolvedTargetComparer.Instance);
            AssertList(list, new List<ResolvedTarget>
            {
                new("x", null, addr1),
                new("c", null, addr2),
                new("b", null, addr3),
            });
        }

        [Fact(DisplayName = "LowestAddressJoinDecider should join existing cluster immediately")]
        public async Task LowestAddressJoinDeciderShouldJoinExistingClusterImmediately()
        {
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var now = DateTimeOffset.Now;
            var addr = new Address("akka", "join-decider-spec-system", "10.0.0.2", 2552);
            var info = new SeedNodesInformation(
                currentTime: now,
                contactPointsChangedAt: now - TimeSpan.FromSeconds(2),
                contactPoints: new[] {ContactA, ContactB, ContactC}.ToImmutableHashSet(),
                seedNodesObservations: new[]
                {
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactA,
                        addr,
                        new[] { addr }.ToImmutableHashSet())
                }.ToImmutableHashSet());
            (await decider.Decide(info)).Should().BeEquivalentTo(new JoinOtherSeedNodes(new []{addr}.ToImmutableHashSet()));
        }

        [Fact(DisplayName = "LowestAddressJoinDecider should keep probing when contact points changed within stable-margin")]
        public async Task KeepProbingWhenContactPointsChangedWithinStableMargin()
        {
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var now = DateTimeOffset.Now;
            var info = new SeedNodesInformation(
                currentTime: now,
                contactPointsChangedAt: now - TimeSpan.FromSeconds(2), // << 2 < stable-margin
                contactPoints: new[] {ContactA, ContactB, ContactC}.ToImmutableHashSet(),
                seedNodesObservations: new[]
                {
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactA,
                        new Address("akka", "join-decider-spec-system", "10.0.0.2", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactB,
                        new Address("akka", "join-decider-spec-system", "b", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactC,
                        new Address("akka", "join-decider-spec-system", "c", 2552),
                        ImmutableHashSet<Address>.Empty),
                }.ToImmutableHashSet());
            (await decider.Decide(info)).Should().Be(KeepProbing.Instance);
        }

        [Fact(DisplayName = "LowestAddressJoinDecider should keep probing when not enough contact points")]
        public async Task KeepProbingWhenNotEnoughContactPoint()
        {
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var now = DateTimeOffset.Now;
            var info = new SeedNodesInformation(
                currentTime: now,
                contactPointsChangedAt: now - TimeSpan.FromSeconds(2), 
                contactPoints: new[] {ContactA, ContactB}.ToImmutableHashSet(), // << 2 < required-contact-point-nr
                seedNodesObservations: new[]
                {
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactA,
                        new Address("akka", "join-decider-spec-system", "10.0.0.2", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactB,
                        new Address("akka", "join-decider-spec-system", "b", 2552),
                        ImmutableHashSet<Address>.Empty),
                }.ToImmutableHashSet());
            (await decider.Decide(info)).Should().Be(KeepProbing.Instance);
        }

        [Fact(DisplayName = "LowestAddressJoinDecider should keep probing when not enough confirmed contact points")]
        public async Task KeepProbingWhenNotEnoughConfirmedContactPoint()
        {
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var now = DateTimeOffset.Now;
            var info = new SeedNodesInformation(
                currentTime: now,
                contactPointsChangedAt: now - TimeSpan.FromSeconds(2), 
                contactPoints: new[] {ContactA, ContactB, ContactC}.ToImmutableHashSet(),
                seedNodesObservations: new[]
                {
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactA,
                        new Address("akka", "join-decider-spec-system", "10.0.0.2", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactB,
                        new Address("akka", "join-decider-spec-system", "b", 2552),
                        ImmutableHashSet<Address>.Empty),
                }.ToImmutableHashSet()); // << 2 < required-contact-point-nr
            (await decider.Decide(info)).Should().Be(KeepProbing.Instance);
        }
        
        [Fact(DisplayName = "LowestAddressJoinDecider should join self when all conditions met and self has the lowest address")]
        public async Task JoinSelfWhenAllConditionsMetAndSelfHasTheLowestAddress()
        {
            _settings!.NewClusterEnabled.Should().BeTrue();
            ClusterBootstrap.Get(System!).SetSelfContactPoint(new Uri($"http://10.0.0.2:{ManagementPort}/test"));
            var decider = new LowestAddressJoinDecider(System!, _settings);
            var now = DateTimeOffset.Now;
            var info = new SeedNodesInformation(
                currentTime: now,
                contactPointsChangedAt: now - TimeSpan.FromSeconds(6), 
                contactPoints: new[] {ContactA, ContactB, ContactC}.ToImmutableHashSet(),
                seedNodesObservations: new[]
                {
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactA,
                        new Address("akka", "join-decider-spec-system", "10.0.0.2", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactB,
                        new Address("akka", "join-decider-spec-system", "b", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactC,
                        new Address("akka", "join-decider-spec-system", "c", 2552),
                        ImmutableHashSet<Address>.Empty),
                }.ToImmutableHashSet());
            (await decider.Decide(info)).Should().Be(JoinSelf.Instance);
        }
        
    }

    public class SelfAwareJoinDeciderIPv6Spec : JoinDeciderSpec
    {
        private static readonly Config Disabled = ConfigurationFactory
            .ParseString("akka.management.cluster.bootstrap.new-cluster-enabled=off");
        
        protected override int RemotingPort => 0;

        private readonly ResolvedTarget _contactIPv6A = new(
            host: "240b-c0e0-202-5e2b-b424-2-0-450.default.pod.cluster.local",
            port: null,
            address: IPAddress.Parse("240b:c0e0:202:5e2b:b424:2:0:450"));

        private readonly ResolvedTarget _contactIPv6B = new(
            host: "240b-c0e0-202-5e2b-b424-2-0-cc4.default.pod.cluster.local",
            port: null,
            address: IPAddress.Parse("240b:c0e0:202:5e2b:b424:2:0:cc4"));

        private readonly ResolvedTarget _contactIPv6C = new(
            host: "240b-c0e0-202-5e2b-b424-2-0-cc5.default.pod.cluster.local",
            port: null,
            address: IPAddress.Parse("240b:c0e0:202:5e2b:b424:2:0:cc5"));

        private readonly SeedNodesInformation _seedNodes; 
        private ClusterBootstrapSettings? _settings;

        public SelfAwareJoinDeciderIPv6Spec(ITestOutputHelper output) : base(Disabled, "join-decider-spec-system-selfaware-ipv6", output)
        {
            var now = DateTimeOffset.Now;
            _seedNodes = new SeedNodesInformation(
                currentTime: now,
                contactPointsChangedAt: now - TimeSpan.FromSeconds(6), 
                contactPoints: new[] {ContactA, ContactB, ContactC}.ToImmutableHashSet(),
                seedNodesObservations: new[]
                {
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        _contactIPv6A,
                        new Address("akka", "join-decider-spec-system-selfaware-ipv6", "[240b:c0e0:202:5e2b:b424:2:0:450]", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        _contactIPv6B,
                        new Address("akka", "join-decider-spec-system-selfaware-ipv6", "[240b:c0e0:202:5e2b:b424:2:0:cc4]", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        _contactIPv6C,
                        new Address("akka", "join-decider-spec-system-selfaware-ipv6", "c", 2552),
                        ImmutableHashSet<Address>.Empty),
                }.ToImmutableHashSet());
        }

        [Fact(DisplayName = "SelfAwareJoinDecider should return true if a target matches selfContactPoint")]
        public void ReturnTrueIfATargetMatchesSelfContactPoint()
        {
            ClusterBootstrap.Get(System!).SetSelfContactPoint(new Uri($"http://[240b:c0e0:202:5e2b:b424:2:0:450]:{ManagementPort}/test"));
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var selfContactPoint = decider.SelfContactPoint();
            var info = _seedNodes;
            var targetList = info.SeedNodesObservations.Select(o => o.ContactPoint).ToList();
            targetList.Sort(ResolvedTargetComparer.Instance);
            var target = targetList.First();
            decider.MatchesSelf(target, selfContactPoint).Should().BeTrue();
        }
        
        [Fact(DisplayName = "SelfAwareJoinDecider should be able to join self if all conditions met")]
        public void BeAbleToJoinSelfIfAllConditionsMet()
        {
            ClusterBootstrap.Get(System!).SetSelfContactPoint(new Uri($"http://[240b:c0e0:202:5e2b:b424:2:0:450]:{ManagementPort}/test"));
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var info = _seedNodes;
            var targetList = info.SeedNodesObservations.Select(o => o.ContactPoint).ToList();
            targetList.Sort(ResolvedTargetComparer.Instance);
            var target = targetList.First();
            decider.CanJoinSelf(target, info).Should().BeTrue();
        }
        
        [Fact(DisplayName = "SelfAwareJoinDecider should not join self if `new-cluster-enabled=off`, even if all conditions met")]
        public async Task NotJoinSelfIfNewClusterEnabledIsSetToOffEvenWhenAllConditionsMet()
        {
            _settings!.NewClusterEnabled.Should().BeFalse();
            ClusterBootstrap.Get(System!).SetSelfContactPoint(new Uri($"http://10.0.0.2:{ManagementPort}/test"));
            var decider = new LowestAddressJoinDecider(System!, _settings);
            (await decider.Decide(_seedNodes)).Should().Be(KeepProbing.Instance);
        }

        protected override Task Start()
        {
            _settings = ClusterBootstrapSettings.Create(System!.Settings.Config, Log!);
            return Task.CompletedTask;
        }
    }
    
    public class SelfAwareJoinDeciderSpec : JoinDeciderSpec
    {
        private static readonly Config Disabled = ConfigurationFactory
            .ParseString("akka.management.cluster.bootstrap.new-cluster-enabled=off");
        
        protected override int RemotingPort => 0;

        private readonly SeedNodesInformation _seedNodes; 
        private ClusterBootstrapSettings? _settings;

        public SelfAwareJoinDeciderSpec(ITestOutputHelper output) : base(Disabled, "join-decider-spec-system-selfaware", output)
        {
            var now = DateTimeOffset.Now;
            _seedNodes = new SeedNodesInformation(
                currentTime: now,
                contactPointsChangedAt: now - TimeSpan.FromSeconds(6), 
                contactPoints: new[] {ContactA, ContactB, ContactC}.ToImmutableHashSet(),
                seedNodesObservations: new[]
                {
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactA,
                        new Address("akka", "join-decider-spec-system-selfaware", "10.0.0.2", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactB,
                        new Address("akka", "join-decider-spec-system-selfaware", "b", 2552),
                        ImmutableHashSet<Address>.Empty),
                    new SeedNodesObservation(
                        now - TimeSpan.FromSeconds(1),
                        ContactC,
                        new Address("akka", "join-decider-spec-system-selfaware", "c", 2552),
                        ImmutableHashSet<Address>.Empty),
                }.ToImmutableHashSet());
        }

        [Fact(DisplayName = "SelfAwareJoinDecider (IPv6) should return true if a target matches selfContactPoint")]
        public void ReturnTrueIfATargetMatchesSelfContactPoint()
        {
            ClusterBootstrap.Get(System!).SetSelfContactPoint(new Uri($"http://10.0.0.2:{ManagementPort}/test"));
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var selfContactPoint = decider.SelfContactPoint();
            var info = _seedNodes;
            var targetList = info.SeedNodesObservations.Select(o => o.ContactPoint).ToList();
            targetList.Sort(ResolvedTargetComparer.Instance);
            var target = targetList.First();
            decider.MatchesSelf(target, selfContactPoint).Should().BeTrue();
        }
        
        [Fact(DisplayName = "SelfAwareJoinDecider (IPv6) should be able to join self if all conditions met")]
        public void BeAbleToJoinSelfIfAllConditionsMet()
        {
            ClusterBootstrap.Get(System!).SetSelfContactPoint(new Uri($"http://10.0.0.2:{ManagementPort}/test"));
            var decider = new LowestAddressJoinDecider(System!, _settings!);
            var info = _seedNodes;
            var targetList = info.SeedNodesObservations.Select(o => o.ContactPoint).ToList();
            targetList.Sort(ResolvedTargetComparer.Instance);
            var target = targetList.First();
            decider.CanJoinSelf(target, info).Should().BeTrue();
        }
        
        [Fact(DisplayName = "SelfAwareJoinDecider (IPv6) should not join self if `new-cluster-enabled=off`, even if all conditions met")]
        public async Task NotJoinSelfIfNewClusterEnabledIsSetToOffEvenWhenAllConditionsMet()
        {
            _settings!.NewClusterEnabled.Should().BeFalse();
            ClusterBootstrap.Get(System!).SetSelfContactPoint(new Uri($"http://10.0.0.2:{ManagementPort}/test"));
            var decider = new LowestAddressJoinDecider(System!, _settings);
            (await decider.Decide(_seedNodes)).Should().Be(KeepProbing.Instance);
        }

        protected override Task Start()
        {
            _settings = ClusterBootstrapSettings.Create(System!.Settings.Config, Log!);
            return Task.CompletedTask;
        }
    }
    
}