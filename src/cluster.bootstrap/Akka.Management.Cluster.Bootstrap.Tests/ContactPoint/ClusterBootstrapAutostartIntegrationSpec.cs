//-----------------------------------------------------------------------
// <copyright file="ClusterBootstrapAutostartIntegrationSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Event;
using Akka.TestKit;
using Akka.TestKit.Xunit2.Internals;
using Akka.Util.Internal;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Management.Cluster.Bootstrap.Tests.ContactPoint
{
    public class ClusterBootstrapAutostartIntegrationSpec : TestKit.Xunit2.TestKit
    {
        private static readonly Config BaseConfig = 
            ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port = 0");
        private const int ClusterSize = 3;
        private const int ScaledSize = 3;
        
        private readonly ImmutableDictionary<string, int> _remotingPorts = ImmutableDictionary<string, int>.Empty;
        private readonly ImmutableDictionary<string, int> _contactPointPorts = ImmutableDictionary<string, int>.Empty;

        private readonly ITestOutputHelper _output;

        private readonly ImmutableList<string> _ids = ImmutableList<string>.Empty;
        private readonly ImmutableList<ActorSystem> _systems = ImmutableList<ActorSystem>.Empty;
        private readonly ImmutableList<ActorSystem> _scaledDownSystems;
        private readonly ImmutableList<Akka.Cluster.Cluster> _clusters = ImmutableList<Akka.Cluster.Cluster>.Empty;
        private readonly int _terminatedSystemCount;
        
        public ClusterBootstrapAutostartIntegrationSpec(ITestOutputHelper output)
            : base(BaseConfig, nameof(ClusterBootstrapAutostartIntegrationSpec), output)
        {
            _output = output;
            
            for (var i = 0; i < ClusterSize; i++)
            {
                _ids = _ids.Add(Guid.NewGuid().ToString()[..8]);
            }

            var sysName = "ClusterBootstrapAutostartIntegrationSpec";
            var targets = new List<ServiceDiscovery.ResolvedTarget>();
            foreach (var id in _ids)
            {
                _remotingPorts = _remotingPorts.Add(id, SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);
                _contactPointPorts = _contactPointPorts.Add(id, SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);
                
                var system = ActorSystem.Create(sysName, Config(id));
                _systems = _systems.Add(system);
                var logger = ((ExtendedActorSystem)system).SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), $"log-test-{id}");
                logger.Tell(new InitializeLogger(system.EventStream));

                var cluster = Akka.Cluster.Cluster.Get(system);
                _clusters = _clusters.Add(cluster);
                
                targets.Add(new ServiceDiscovery.ResolvedTarget(
                    host: cluster.SelfAddress.Host, 
                    port: _contactPointPorts[id], 
                    address: IPAddress.Parse(cluster.SelfAddress.Host)));
            }
            
            // prepare the "mock DNS"
            var name = "service.svc.cluster.local";
            MockDiscovery.Set(
                new Lookup(name, "management-auto", "tcp2"),
                () => Task.FromResult(new ServiceDiscovery.Resolved(name, targets)));
            
            _terminatedSystemCount = ClusterSize - ScaledSize;
            _scaledDownSystems = _systems.Take(ScaledSize).ToImmutableList();
        }
        
        private Config Config(string id)
        {
            var managementPort = _contactPointPorts[id];
            var remotingPort = _remotingPorts[id];
            
            _output.WriteLine($"System [{id}]: management port: {managementPort}");
            _output.WriteLine($"System [{id}]:   remoting port: {remotingPort}");

            return ConfigurationFactory.ParseString($@"
                akka {{
                    loglevel = INFO
                    # trigger autostart by loading the extension through config
                    extensions = [""Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap""]
                    actor.provider = cluster

                    # this can be referred to in tests to use the mock discovery implementation
                    discovery.mock-dns.class = ""Akka.Management.Cluster.Bootstrap.Tests.MockDiscovery, Akka.Management.Cluster.Bootstrap.Tests""
                    
                    remote.dot-netty.tcp.hostname = ""127.0.0.1""
                    remote.dot-netty.tcp.port = {remotingPort}

                    management {{
                        http.port = {managementPort}
                        http.hostname = ""127.0.0.1""
                        cluster.bootstrap {{
                            contact-point-discovery {{
                                discovery-method = mock-dns
                                service-name = ""service""
                                port-name = ""management-auto""
                                protocol = ""tcp2""
                                service-namespace = ""svc.cluster.local""
                                stable-margin = 4s
                            }}
                        }}
                    }}
                }}")
                .WithFallback(TestKitBase.DefaultConfig);
        }

        [Fact(DisplayName = "Cluster Bootstrap auto start integration test")]
        public void StartSpec()
        {
            HoconShouldBeInjected();
            _output.WriteLine("=== Starting JoinDiscoveredDns()");
            JoinDiscoveredDns();
            _output.WriteLine("=== JoinDiscoveredDns() Success");
            _output.WriteLine("=== Starting TerminateOnAutostartFail()");
            TerminateOnAutostartFail();
            _output.WriteLine("=== TerminateOnAutostartFail() Success");
            _output.WriteLine("=== Starting ScaleDown()");
            ScaleDown();
            _output.WriteLine("=== ScaleDown() Success");
            _output.WriteLine("=== Starting Terminate()");
            Terminate();
            _output.WriteLine("=== Terminate() Success");
        }
        
        // Default hocon settings should be injected automatically when the module is started
        private void HoconShouldBeInjected()
        {
            var exception = Record.Exception(() =>
            {
                var _ = ClusterBootstrapSettings.Create(_systems[0].Settings.Config, NoLogger.Instance);
            });
            exception.Should().BeNull();
        }

        // join three DNS discovered nodes by forming new cluster (happy path)
        private void JoinDiscoveredDns()
        {
            var probeList = new List<(TestProbe, IActorRef)>();
            foreach (var system in _systems)
            {
                // grab cluster bootstrap coordinator actor
                var coordinatorProbe = CreateTestProbe(system);
                var selection =
                    system.ActorSelection("akka://ClusterBootstrapAutostartIntegrationSpec/system/bootstrapCoordinator");
                IActorRef? coordinator = null;
                AwaitAssert(() =>
                {
                    selection.Tell(new Identify(null));
                    var response = ExpectMsg<ActorIdentity>(TimeSpan.FromSeconds(10));
                    coordinator = response.Subject;
                    coordinator.Should().NotBeNull();
                    coordinatorProbe.Watch(coordinator);
                });
                probeList.Add((coordinatorProbe, coordinator!));
            }

            // All nodes should join
            var probe = CreateTestProbe(_systems[0]);
            var cluster = _clusters[0];
            probe.AwaitAssert(() =>
            {
                cluster.State.Members.Count.Should().Be(ClusterSize);
                cluster.State.Members.Count(m => m.Status == MemberStatus.Up).Should().Be(ClusterSize);
            }, RemainingOrDefault * ClusterSize * 2);
            
            // cluster bootstrap coordinator should stop after joining
            foreach (var tuple in probeList)
            {
                var (coordinatorProbe, coordinator) = tuple;
                coordinatorProbe.ExpectTerminated(coordinator);
                ((RepointableActorRef) coordinator).Children.ToList().Count.Should().Be(0);
            }
        }
        
        // terminate a system when autostart fails
        private void TerminateOnAutostartFail()
        {
            var terminated = false;
            
            // failing because we re-use the same port for management here (but not for remoting
            // as that itself terminates the system on start)
            var systemA = ActorSystem.Create(
                "System",
                ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port = 0")
                    .WithFallback(Config(_ids[0])));
            
            systemA.WhenTerminated.ContinueWith(_ => terminated = true);
            AwaitCondition(() => terminated, TimeSpan.FromSeconds(10));
        }

        private void ScaleDown()
        {
            if (_terminatedSystemCount == 0)
            {
                _output.WriteLine("===== Cluster size is equal to scaled cluster size. Skipping ScaleDown().");
                return;
            }
            
            var counter = new AtomicCounter(0);
            
            _systems
                .Where(system => !_scaledDownSystems.Contains(system))
                .ForEach(system =>
                {
                    CoordinatedShutdown.Get(system).Run(CoordinatedShutdown.ClrExitReason.Instance)
                        .ContinueWith(_ =>
                        {
                            counter.GetAndIncrement();
                        });
                });
            
            AwaitCondition(() => counter.Current == _terminatedSystemCount, RemainingOrDefault * _terminatedSystemCount);
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        private void Terminate()
        {
            var counter = new AtomicCounter(0);
            _scaledDownSystems
                .ForEach(system => 
                    CoordinatedShutdown.Get(system).Run(CoordinatedShutdown.ClrExitReason.Instance)
                        .ContinueWith(_ => counter.GetAndIncrement())); 
            
            AwaitCondition(() => counter.Current == ScaledSize, RemainingOrDefault * ScaledSize);
        }
    }
}