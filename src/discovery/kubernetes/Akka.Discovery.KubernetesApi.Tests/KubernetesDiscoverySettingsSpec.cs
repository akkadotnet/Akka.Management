// -----------------------------------------------------------------------
//  <copyright file="KubernetesDiscoverySettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Xunit;

namespace Akka.Discovery.KubernetesApi.Tests
{
    public class KubernetesDiscoverySettingsSpec
    {
        [Fact(DisplayName = "Default settings should contain default values")]
        public void DefaultSettingsTest()
        {
            var settings = KubernetesDiscoverySettings.Create(KubernetesDiscovery.DefaultConfiguration()
                .GetConfig("akka.discovery.kubernetes-api"));

            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt", settings.ApiCaPath);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/token", settings.ApiTokenPath);
            Assert.Equal("KUBERNETES_SERVICE_HOST", settings.ApiServiceHostEnvName);
            Assert.Equal("KUBERNETES_SERVICE_PORT", settings.ApiServicePortEnvName);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/namespace", settings.PodNamespacePath);
            Assert.Null(settings.PodNamespace);
            Assert.False(settings.AllNamespaces);
            Assert.Equal("cluster.local", settings.PodDomain);
            Assert.Equal("app=a", settings.PodLabelSelector("a"));
            Assert.True(settings.RawIp);
            Assert.Null(settings.ContainerName);
        }

        [Fact(DisplayName = "Empty settings should contain default values")]
        public void EmptySettingsTest()
        {
            var empty = KubernetesDiscoverySettings.Empty;
            var settings = KubernetesDiscoverySettings.Create(KubernetesDiscovery.DefaultConfiguration()
                .GetConfig("akka.discovery.kubernetes-api"));

            Assert.Equal(settings.ApiCaPath, empty.ApiCaPath);
            Assert.Equal(settings.ApiTokenPath, empty.ApiTokenPath);
            Assert.Equal(settings.ApiServiceHostEnvName, empty.ApiServiceHostEnvName);
            Assert.Equal(settings.ApiServicePortEnvName, empty.ApiServicePortEnvName);
            Assert.Equal(settings.PodNamespacePath, empty.PodNamespacePath);
            Assert.Equal(settings.PodNamespace, empty.PodNamespace);
            Assert.Equal(settings.AllNamespaces, empty.AllNamespaces);
            Assert.Equal(settings.PodDomain, empty.PodDomain);
            Assert.Equal(settings.PodLabelSelector("a"), empty.PodLabelSelector("a"));
            Assert.Equal(settings.RawIp, empty.RawIp);
            Assert.Equal(settings.ContainerName, empty.ContainerName);
        }

        [Fact(DisplayName = "Settings With override must work")]
        public void WithOverrideTest()
        {
            var settings = KubernetesDiscoverySettings.Empty
                .WithApiCaPath("a")
                .WithApiTokenPath("b")
                .WithApiServiceHostEnvName("c")
                .WithApiServicePortEnvName("d")
                .WithPodNamespacePath("e")
                .WithPodNamespace("f")
                .WithAllNamespaces(true)
                .WithPodDomain("g")
                .WithPodLabelSelector("h={0}")
                .WithRawIp(false)
                .WithContainerName("i");
                
            Assert.Equal("a", settings.ApiCaPath);
            Assert.Equal("b", settings.ApiTokenPath);
            Assert.Equal("c", settings.ApiServiceHostEnvName);
            Assert.Equal("d", settings.ApiServicePortEnvName);
            Assert.Equal("e", settings.PodNamespacePath);
            Assert.Equal("f", settings.PodNamespace);
            Assert.True(settings.AllNamespaces);
            Assert.Equal("g", settings.PodDomain);
            Assert.Equal("h=a", settings.PodLabelSelector("a"));
            Assert.False(settings.RawIp);
            Assert.Equal("i", settings.ContainerName);
        }

        [Fact(DisplayName = "Setup override should work")]
        public void SetupOverrideTest()
        {
            var setup = new KubernetesDiscoverySetup
            {
                ApiCaPath  = "a",
                ApiTokenPath  = "b",
                ApiServiceHostEnvName  = "c",
                ApiServicePortEnvName  = "d",
                PodNamespacePath  = "e",
                PodNamespace  = "f",
                AllNamespaces = true,
                PodDomain  = "g",
                PodLabelSelector  = "h={0}",
                RawIp  = false,
                ContainerName  = "i"
            };
            var settings = setup.Apply(KubernetesDiscoverySettings.Empty);
                
            Assert.Equal("a", settings.ApiCaPath);
            Assert.Equal("b", settings.ApiTokenPath);
            Assert.Equal("c", settings.ApiServiceHostEnvName);
            Assert.Equal("d", settings.ApiServicePortEnvName);
            Assert.Equal("e", settings.PodNamespacePath);
            Assert.Equal("f", settings.PodNamespace);
            Assert.True(settings.AllNamespaces);
            Assert.Equal("g", settings.PodDomain);
            Assert.Equal("h=a", settings.PodLabelSelector("a"));
            Assert.False(settings.RawIp);
            Assert.Equal("i", settings.ContainerName);
        }
        
        [Fact(DisplayName = "Setup override should work inside the module")]
        public void ModuleSetupTest()
        {
            var setup = ActorSystemSetup.Empty
                .And(new KubernetesDiscoverySetup
                {
                    ApiCaPath = "a",
                    ApiTokenPath = "b",
                    ApiServiceHostEnvName = "c",
                    ApiServicePortEnvName = "d",
                    PodNamespacePath = "e",
                    PodNamespace = "f",
                    AllNamespaces = true,
                    PodDomain = "g",
                    PodLabelSelector = "h={0}",
                    RawIp = false,
                    ContainerName = "i"
                });

            using (var sys = ActorSystem.Create(nameof(KubernetesDiscoverySettingsSpec), setup))
            {
                // TODO: KubernetesDiscovery.Settings is obsolete since 1.5.26, but this test deliberately
                // verifies that a KubernetesDiscoverySetup is applied to the resolved settings — behavior
                // that only the extension performs. KubernetesDiscoverySettings.Create(sys) reads config
                // only and would not apply the Setup, so we intentionally exercise the obsolete member here.
#pragma warning disable CS0618 // Type or member is obsolete
                var settings = KubernetesDiscovery.Get(sys).Settings;
#pragma warning restore CS0618

                Assert.Equal("a", settings.ApiCaPath);
                Assert.Equal("b", settings.ApiTokenPath);
                Assert.Equal("c", settings.ApiServiceHostEnvName);
                Assert.Equal("d", settings.ApiServicePortEnvName);
                Assert.Equal("e", settings.PodNamespacePath);
                Assert.Equal("f", settings.PodNamespace);
                Assert.True(settings.AllNamespaces);
                Assert.Equal("g", settings.PodDomain);
                Assert.Equal("h=a", settings.PodLabelSelector("a"));
                Assert.False(settings.RawIp);
                Assert.Equal("i", settings.ContainerName);
            }
        }
    }
}