using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Event;
using Akka.TestKit;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Management.Cluster.Bootstrap.Tests.ContactPoint
{
    public class ClusterBootstrapAutostartIntegrationSpec : TestKit.Xunit2.TestKit, IAsyncLifetime
    {
        private ImmutableDictionary<string, int> _remotingPorts = ImmutableDictionary<string, int>.Empty;
        private ImmutableDictionary<string, int> _contactPointPorts = ImmutableDictionary<string, int>.Empty;

        private readonly ITestOutputHelper _output;

        private readonly ActorSystem _systemA; 
        private readonly ActorSystem _systemB; 
        private readonly ActorSystem _systemC;

        private readonly Akka.Cluster.Cluster _clusterA;
        private readonly Akka.Cluster.Cluster _clusterB;
        private readonly Akka.Cluster.Cluster _clusterC;
        
        public ClusterBootstrapAutostartIntegrationSpec(ITestOutputHelper output)
        {
            _output = output;

            _remotingPorts = _remotingPorts.Add("A", SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);
            _remotingPorts = _remotingPorts.Add("B", SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);
            _remotingPorts = _remotingPorts.Add("C", SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);
            
            _contactPointPorts = _contactPointPorts.Add("A", SocketUtil.TemporaryTcpAddress("127.0.0.1").Port); 
            _contactPointPorts = _contactPointPorts.Add("B", SocketUtil.TemporaryTcpAddress("127.0.0.1").Port); 
            _contactPointPorts = _contactPointPorts.Add("C", SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);

            var sysName = "ClusterBootstrapAutostartIntegrationSpec";
            _systemA = ActorSystem.Create(sysName, Config("A"));
            var logger = ((ExtendedActorSystem)_systemA).SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Tell(new InitializeLogger(_systemA.EventStream));
            
            _systemB = ActorSystem.Create(sysName, Config("B"));
            logger = ((ExtendedActorSystem)_systemB).SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Tell(new InitializeLogger(_systemB.EventStream));
            
            _systemC = ActorSystem.Create(sysName, Config("C"));
            logger = ((ExtendedActorSystem)_systemC).SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Tell(new InitializeLogger(_systemC.EventStream));
            
            _clusterA = Akka.Cluster.Cluster.Get(_systemA);
            _clusterB = Akka.Cluster.Cluster.Get(_systemB);
            _clusterC = Akka.Cluster.Cluster.Get(_systemC);
            
            // prepare the "mock DNS"
            var name = "service.svc.cluster.local";
            MockDiscovery.Set(
                new Lookup(name, "management-auto", "tcp2"),
                () => Task.FromResult(new ServiceDiscovery.Resolved(
                    name,
                    new []
                    {
                        new ServiceDiscovery.ResolvedTarget(
                            host: _clusterA.SelfAddress.Host,
                            port: _contactPointPorts["A"],
                            address: IPAddress.Parse(_clusterA.SelfAddress.Host)),
                        new ServiceDiscovery.ResolvedTarget(
                            host: _clusterB.SelfAddress.Host,
                            port: _contactPointPorts["B"],
                            address: IPAddress.Parse(_clusterB.SelfAddress.Host)),
                        new ServiceDiscovery.ResolvedTarget(
                            host: _clusterC.SelfAddress.Host,
                            port: _contactPointPorts["C"],
                            address: IPAddress.Parse(_clusterC.SelfAddress.Host)),
                    })));
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
                .WithFallback(ClusterBootstrap.DefaultConfiguration())
                .WithFallback(AkkaManagementProvider.DefaultConfiguration())
                .WithFallback(TestKitBase.DefaultConfig);
        }

        [Fact(DisplayName = "Cluster Bootstrap auto start integration test")]
        public async Task StartSpec()
        {
            await JoinDiscoveredDns();
            await TerminateOnAutostartFail();
        }
        
        // join three DNS discovered nodes by forming new cluster (happy path)
        private async Task JoinDiscoveredDns()
        {
            var pA = CreateTestProbe(_systemA);
            await pA.AwaitAssertAsync(() =>
            {
                _clusterA.State.Members.Count.Should().Be(3);
                _clusterA.State.Members.Count(m => m.Status == MemberStatus.Up).Should().Be(3);
            }, TimeSpan.FromSeconds(20));
        }
        
        // terminate a system when autostart fails
        private async Task TerminateOnAutostartFail()
        {
            // failing because we re-use the same port for management here (but not for remoting
            // as that itself terminates the system on start)
            var systemA = ActorSystem.Create(
                "System",
                ConfigurationFactory.ParseString("akka.remote.dot-netty.tcp.port = 0")
                    .WithFallback(Config("A")));
            await systemA.WhenTerminated;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            var tasks = new[]
            {
                CoordinatedShutdown.Get(_systemA).Run(CoordinatedShutdown.ClrExitReason.Instance),
                CoordinatedShutdown.Get(_systemB).Run(CoordinatedShutdown.ClrExitReason.Instance),
                CoordinatedShutdown.Get(_systemC).Run(CoordinatedShutdown.ClrExitReason.Instance),
            };

            await Task.WhenAll(tasks);
        }
    }
}