// -----------------------------------------------------------------------
//  <copyright file="KubernetesDiscoverySettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using FluentAssertions;
using Xunit;
using static FluentAssertions.FluentActions;

namespace Akka.Discovery.KubernetesApi.Tests
{
    public class KubernetesDiscoverySettingsSpec
    {
        [Fact(DisplayName = "Default settings should contain default values")]
        public void DefaultSettingsTest()
        {
            var settings = KubernetesDiscoverySettings.Create(KubernetesDiscovery.DefaultConfiguration()
                .GetConfig("akka.discovery.kubernetes-api"));

            settings.ApiCaPath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt");
            settings.ApiTokenPath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/token");
            settings.ApiServiceHostEnvName.Should().Be("KUBERNETES_SERVICE_HOST");
            settings.ApiServicePortEnvName.Should().Be("KUBERNETES_SERVICE_PORT");
            settings.PodNamespacePath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/namespace");
            settings.PodNamespace.Should().BeNull();
            settings.PodDomain.Should().Be("cluster.local");
            settings.PodLabelSelector("a").Should().Be("app=a");
            settings.RawIp.Should().BeTrue();
            settings.ContainerName.Should().BeNull();
        }

        [Fact(DisplayName = "Empty settings should contain default values")]
        public void EmptySettingsTest()
        {
            var empty = KubernetesDiscoverySettings.Empty;
            var settings = KubernetesDiscoverySettings.Create(KubernetesDiscovery.DefaultConfiguration()
                .GetConfig("akka.discovery.kubernetes-api"));

            empty.ApiCaPath.Should().Be(settings.ApiCaPath);
            empty.ApiTokenPath.Should().Be(settings.ApiTokenPath);
            empty.ApiServiceHostEnvName.Should().Be(settings.ApiServiceHostEnvName);
            empty.ApiServicePortEnvName.Should().Be(settings.ApiServicePortEnvName);
            empty.PodNamespacePath.Should().Be(settings.PodNamespacePath);
            empty.PodNamespace.Should().Be(settings.PodNamespace);
            empty.PodDomain.Should().Be(settings.PodDomain);
            empty.PodLabelSelector("a").Should().Be(settings.PodLabelSelector("a"));
            empty.RawIp.Should().Be(settings.RawIp);
            empty.ContainerName.Should().Be(settings.ContainerName);
        }

        [Fact(DisplayName = "Illegal pod-label-selector must throw")]
        public void IllegalPodLabelSelectorTest()
        {
            var settings = KubernetesDiscoverySettings.Empty;

            Invoking(() => settings.WithPodLabelSelector("={0}"))
                .Should().ThrowExactly<ConfigurationException>();
            
            Invoking(() => settings.WithPodLabelSelector("a="))
                .Should().ThrowExactly<ConfigurationException>();
            
            Invoking(() => settings.WithPodLabelSelector("a=={0}"))
                .Should().ThrowExactly<ConfigurationException>();
            
            Invoking(() => settings.WithPodLabelSelector("a{1}={0}"))
                .Should().ThrowExactly<ConfigurationException>();
            
            Invoking(() => settings.WithPodLabelSelector("a={0}b"))
                .Should().ThrowExactly<ConfigurationException>();
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
                .WithPodDomain("g")
                .WithPodLabelSelector("h={0}")
                .WithRawIp(false)
                .WithContainerName("i");
                
            settings.ApiCaPath.Should().Be("a");
            settings.ApiTokenPath.Should().Be("b");
            settings.ApiServiceHostEnvName.Should().Be("c");
            settings.ApiServicePortEnvName.Should().Be("d");
            settings.PodNamespacePath.Should().Be("e");
            settings.PodNamespace.Should().Be("f");
            settings.PodDomain.Should().Be("g");
            settings.PodLabelSelector("a").Should().Be("h=a");
            settings.RawIp.Should().BeFalse();
            settings.ContainerName.Should().Be("i");
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
                PodDomain  = "g",
                PodLabelSelector  = "h={0}",
                RawIp  = false,
                ContainerName  = "i"
            };
            var settings = setup.Apply(KubernetesDiscoverySettings.Empty);
                
            settings.ApiCaPath.Should().Be("a");
            settings.ApiTokenPath.Should().Be("b");
            settings.ApiServiceHostEnvName.Should().Be("c");
            settings.ApiServicePortEnvName.Should().Be("d");
            settings.PodNamespacePath.Should().Be("e");
            settings.PodNamespace.Should().Be("f");
            settings.PodDomain.Should().Be("g");
            settings.PodLabelSelector("a").Should().Be("h=a");
            settings.RawIp.Should().BeFalse();
            settings.ContainerName.Should().Be("i");
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
                    PodDomain = "g",
                    PodLabelSelector = "h={0}",
                    RawIp = false,
                    ContainerName = "i"
                });

            using (var sys = ActorSystem.Create(nameof(KubernetesDiscoverySettingsSpec), setup))
            {
                var settings = KubernetesDiscovery.Get(sys).Settings;
                
                settings.ApiCaPath.Should().Be("a");
                settings.ApiTokenPath.Should().Be("b");
                settings.ApiServiceHostEnvName.Should().Be("c");
                settings.ApiServicePortEnvName.Should().Be("d");
                settings.PodNamespacePath.Should().Be("e");
                settings.PodNamespace.Should().Be("f");
                settings.PodDomain.Should().Be("g");
                settings.PodLabelSelector("a").Should().Be("h=a");
                settings.RawIp.Should().BeFalse();
                settings.ContainerName.Should().Be("i");
            }
        }
    }
}