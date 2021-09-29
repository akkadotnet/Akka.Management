using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Akka.Actor;
using FluentAssertions;
using k8s.Models;
using Microsoft.Rest.Serialization;
using Xunit;

namespace Akka.Discovery.KubernetesApi.Tests
{
    public class KubernetesApiServiceDiscoverySpec
    {
        [Fact(DisplayName = "Targets should calculate the correct list of resolved targets")]
        public void TargetsCalculateCorrectResolvedTargetList()
        {
            var podList = new V1PodList(new List<V1Pod>
            {
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10000, name:"akka-remote"),
                            new V1ContainerPort(10001, name:"management"),
                            new V1ContainerPort(10002, name:"http"),
                        })
                    }),
                    status: new V1PodStatus(podIP: "172.17.0.4", phase:"Running"),
                    metadata: new V1ObjectMeta()),
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10000, name:"akka-remote"),
                            new V1ContainerPort(10001, name:"management"),
                            new V1ContainerPort(10002, name:"http"),
                        })
                    }),
                    status: new V1PodStatus(phase:"Running"),
                    metadata: new V1ObjectMeta()),
            });

            var result =
                KubernetesApiServiceDiscovery.Targets(podList, "management", "default", "cluster.local", false, null);
            result.Should().BeEquivalentTo(new List<ServiceDiscovery.ResolvedTarget>
            {
                new ServiceDiscovery.ResolvedTarget(
                    host: "172-17-0-4.default.pod.cluster.local",
                    port: 10001,
                    address: IPAddress.Parse("172.17.0.4"))
            });
        }

        [Fact(DisplayName = "Targets should ignore deleted pods")]
        public void TargetsIgnoreDeletedPods()
        {
            var podList = new V1PodList(new List<V1Pod>
            {
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10000, name:"akka-remote"),
                            new V1ContainerPort(10001, name:"management"),
                            new V1ContainerPort(10002, name:"http"),
                        })
                    }),
                    status: new V1PodStatus(podIP: "172.17.0.4", phase:"Running"),
                    metadata: new V1ObjectMeta(deletionTimestamp: DateTime.Now)),
            });

            var result =
                KubernetesApiServiceDiscovery.Targets(podList, "management", "default", "cluster.local", false, null);
            result.Should().BeEquivalentTo(new List<ServiceDiscovery.ResolvedTarget>());
        }
        
        // This test allows users to not declare the management port in their container spec,
        // which is not only convenient, it also is required in Istio where ports declared
        // in the container spec are redirected through Envoy, and in Knative, where only
        // one port is allowed to be declared at all (that port being the primary port for
        // the http/grpc service, not the management or remoting ports).
        [Fact(DisplayName = "Targets should return a single result per host with no port when no port name is requested")]
        public void TargetsReturnSingleResultWhenNoPortNameRequested()
        {
            var podList = new V1PodList(new List<V1Pod>
            {
                // Pod with multiple ports
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10000, name:"akka-remote"),
                            new V1ContainerPort(10001, name:"management"),
                            new V1ContainerPort(10002, name:"http"),
                        })
                    }),
                    status: new V1PodStatus(podIP: "172.17.0.4", phase:"Running"),
                    metadata: new V1ObjectMeta()),
                // Pod with no ports
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example")
                    }),
                    status: new V1PodStatus(podIP: "172.17.0.5", phase:"Running"),
                    metadata: new V1ObjectMeta()),
                // Pod with multiple containers
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10000, name:"akka-remote"),
                            new V1ContainerPort(10001, name:"management"),
                        }),
                        new V1Container("sidecar", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10002, name:"http"),
                        }),
                    }),
                    status: new V1PodStatus(podIP: "172.17.0.6", phase:"Running"),
                    metadata: new V1ObjectMeta()),
            });

            var result =
                KubernetesApiServiceDiscovery.Targets(podList, null, "default", "cluster.local", false, null);
            result.Should().BeEquivalentTo(new List<ServiceDiscovery.ResolvedTarget>
            {
                new ServiceDiscovery.ResolvedTarget(
                    host: "172-17-0-4.default.pod.cluster.local",
                    port: null,
                    address: IPAddress.Parse("172.17.0.4")),
                new ServiceDiscovery.ResolvedTarget(
                    host: "172-17-0-5.default.pod.cluster.local",
                    port: null,
                    address: IPAddress.Parse("172.17.0.5")),
                new ServiceDiscovery.ResolvedTarget(
                    host: "172-17-0-6.default.pod.cluster.local",
                    port: null,
                    address: IPAddress.Parse("172.17.0.6")),
            });
        }

        [Fact(DisplayName = "Targets should ignore non-running pods")]
        public void IgnoreNonRunningPods()
        {
            var podList = new V1PodList(new List<V1Pod>
            {
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10000, name:"akka-remote"),
                            new V1ContainerPort(10001, name:"management"),
                            new V1ContainerPort(10002, name:"http"),
                        })
                    }),
                    status: new V1PodStatus(podIP: "172.17.0.4", phase:"Succeeded"),
                    metadata: new V1ObjectMeta()),
            });

            var result =
                KubernetesApiServiceDiscovery.Targets(podList, "management", "default", "cluster.local", false, null);
            result.Should().BeEquivalentTo(new List<ServiceDiscovery.ResolvedTarget>());
        }
        
        [Fact(DisplayName = "Targets should ignore running pods where the container is waiting")]
        public void IgnoreRunningPodsWhereContainerIsWaiting()
        {
            var json = LoadResource("Akka.Discovery.KubernetesApi.Tests.multi-container-pod.json");
            var podList = SafeJsonConvert.DeserializeObject<V1PodList>(json);

            KubernetesApiServiceDiscovery.Targets(
                podList, 
                null,
                "b58dbc88-3651-4fb4-8408-60c375592d1d",
                "cluster.local",
                false, 
                "cloudstate-sidecar")
                .Should().BeEquivalentTo(new List<ServiceDiscovery.ResolvedTarget>());
            
            // Nonsense for this example data, but to check we do find the other containers:
            KubernetesApiServiceDiscovery.Targets(
                    podList, 
                    null,
                    "b58dbc88-3651-4fb4-8408-60c375592d1d",
                    "cluster.local",
                    false, 
                    "user-function")
                .Should().BeEquivalentTo(new List<ServiceDiscovery.ResolvedTarget>
                {
                    new ServiceDiscovery.ResolvedTarget(
                        host: "10-8-7-9.b58dbc88-3651-4fb4-8408-60c375592d1d.pod.cluster.local",
                        port: null,
                        address: IPAddress.Parse("10.8.7.9"))
                });
        }

        [Fact(DisplayName = "Targets should use a ip instead of the host if requested")]
        public void TargetsUseIpIfRequested()
        {
            var podList = new V1PodList(new List<V1Pod>
            {
                new V1Pod(
                    spec: new V1PodSpec(new List<V1Container>
                    {
                        new V1Container("akka-cluster-tooling-example", ports:new List<V1ContainerPort>
                        {
                            new V1ContainerPort(10000, name:"akka-remote"),
                            new V1ContainerPort(10001, name:"management"),
                            new V1ContainerPort(10002, name:"http"),
                        })
                    }),
                    status: new V1PodStatus(podIP: "172.17.0.4", phase:"Running"),
                    metadata: new V1ObjectMeta()),
            });

            var result =
                KubernetesApiServiceDiscovery.Targets(podList, "management", "default", "cluster.local", true, null);
            result.Should().BeEquivalentTo(new List<ServiceDiscovery.ResolvedTarget>
            {
                new ServiceDiscovery.ResolvedTarget(
                    host: "172.17.0.4",
                    port: 10001,
                    address: IPAddress.Parse("172.17.0.4"))
            });
        }

        [Fact(DisplayName = "The discovery loading mechanism should allow loading kubernetes-api discovery even if it is not the default")]
        public async Task DiscoveryMechanismShouldAllowLoadingDiscovery()
        {
            var sys = ActorSystem.Create("test", KubernetesDiscovery.DefaultConfiguration());
            
            // kubernetes-api-discovery
            var discovery = Discovery.Get(sys).LoadServiceDiscovery("kubernetes-api");
            discovery.Should().BeOfType<KubernetesApiServiceDiscovery>();
            await sys.Terminate();
        }

        private string LoadResource(string resourceName)
        {
            var assembly = this.GetType().Assembly;
            using(var stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }            
        }
    }
}