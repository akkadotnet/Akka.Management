//-----------------------------------------------------------------------
// <copyright file="StressTest.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Management.Dsl;
using Akka.Util.Internal;

namespace StressTest
{
    public class StressTest
    {
        private readonly int _clusterSize;
        private readonly int _scaledSize;
        
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
        
        private readonly ImmutableDictionary<string, int> _remotingPorts = ImmutableDictionary<string, int>.Empty;
        private readonly ImmutableDictionary<string, int> _contactPointPorts = ImmutableDictionary<string, int>.Empty;

        private readonly ImmutableList<string> _ids = ImmutableList<string>.Empty;
        private readonly ImmutableList<ActorSystem> _systems = ImmutableList<ActorSystem>.Empty;
        private readonly ImmutableList<ActorSystem> _scaledDownSystems;
        private readonly ImmutableList<Akka.Cluster.Cluster> _clusters = ImmutableList<Akka.Cluster.Cluster>.Empty;
        private readonly int _terminatedSystemCount;
        
        public StressTest(int clusterSize, int scaledSize)
        {
            _clusterSize = clusterSize;
            _scaledSize = scaledSize;
            
            for (var i = 0; i < _clusterSize; i++)
            {
                _ids = _ids.Add(Guid.NewGuid().ToString()[..8]);
            }

            var sysName = "StressTest";
            var targets = new List<ServiceDiscovery.ResolvedTarget>();
            foreach (var id in _ids)
            {
                _remotingPorts = _remotingPorts.Add(id, SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);
                _contactPointPorts = _contactPointPorts.Add(id, SocketUtil.TemporaryTcpAddress("127.0.0.1").Port);
                
                var system = ActorSystem.Create(sysName, Config(id));
                _systems = _systems.Add(system);

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
            
            _terminatedSystemCount = _clusterSize - _scaledSize;
            _scaledDownSystems = _systems.Take(_scaledSize).ToImmutableList();
        }
        
        private Config Config(string id)
        {
            var managementPort = _contactPointPorts[id];
            var remotingPort = _remotingPorts[id];
            
            Console.WriteLine($"System [{id}]: management port: {managementPort}");
            Console.WriteLine($"System [{id}]:   remoting port: {remotingPort}");

            return ConfigurationFactory.ParseString($@"
                akka {{
                    loglevel = INFO
                    # trigger autostart by loading the extension through config
                    extensions = [""Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap""]
                    actor.provider = cluster

                    # this can be referred to in tests to use the mock discovery implementation
                    discovery.mock-dns.class = ""StressTest.MockDiscovery, StressTest""
                    
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
                .WithFallback(AkkaManagementProvider.DefaultConfiguration());
        }

        public async Task StartTest()
        {
            Console.WriteLine($"======= Start forming cluster with {_clusterSize} member nodes.");
            JoinDiscoveredDns();
            Console.WriteLine("======= Cluster successfully created.");
            Console.WriteLine("======= Waiting for 1 minute to see if any unwanted behaviour occurs.");
            await Task.Delay(TimeSpan.FromMinutes(1));
            
            Console.WriteLine($"======= Scaling down cluster to {_scaledSize} member nodes.");
            ScaleDown();
            Console.WriteLine($"======= Cluster scaled down to {_scaledSize} successfully.");
            Console.WriteLine("======= Waiting for 1 minute to see if any unwanted behaviour occurs.");
            await Task.Delay(TimeSpan.FromMinutes(1));

            Console.WriteLine("======= Shutting down all nodes.");
            Terminate();
            Console.WriteLine("======= All nodes shuts down successfully.");
        }
        
        // join three DNS discovered nodes by forming new cluster (happy path)
        private void JoinDiscoveredDns()
        {
            // All nodes should join
            var cluster = _clusters[0];
            
            var complete = AwaitCondition(() =>
                cluster.State.Members.Count == _clusterSize &&
                    cluster.State.Members.Count(m => m.Status == MemberStatus.Up) == _clusterSize, 
                _timeout * _clusterSize);

            if(!complete)
            {
                var count = cluster.State.Members.Count;
                var upCount = cluster.State.Members.Count(m => m.Status == MemberStatus.Up);
                throw new Exception(
                    $"Cluster failed to form after {_timeout * _clusterSize}. " +
                    $"Cluster members: [{count}/{_clusterSize}]. " +
                    $"Cluster up members: [{upCount}/{_clusterSize}]");
            }
        }
        
        private void ScaleDown()
        {
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
            
            var complete = AwaitCondition(() => counter.Current == _terminatedSystemCount, _timeout * _terminatedSystemCount);
            if (!complete)
                throw new Exception($"Cluster failed to scale down from {_clusterSize} to {_scaledSize} nodes " +
                                    $"within {_timeout * _terminatedSystemCount}");
        }

        private void Terminate()
        {
            var counter = new AtomicCounter(0);
            _scaledDownSystems
                .ForEach(system => 
                    CoordinatedShutdown.Get(system).Run(CoordinatedShutdown.ClrExitReason.Instance)
                        .ContinueWith(_ => counter.GetAndIncrement()));

            var complete = AwaitCondition(() => counter.Current == _scaledSize, _timeout * _scaledSize);
            if (!complete)
                throw new Exception($"Cluster did not shut down gracefully within {_timeout * _scaledSize}");
        }

        private bool AwaitCondition(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = timeout.TotalMilliseconds;
            var complete = false;
            var watch = Stopwatch.StartNew();
            while (!complete && watch.ElapsedMilliseconds < deadline)
            {
                complete = condition();
                Thread.Sleep(100);
            }
            watch.Stop();
            return complete;
        }
    }
}