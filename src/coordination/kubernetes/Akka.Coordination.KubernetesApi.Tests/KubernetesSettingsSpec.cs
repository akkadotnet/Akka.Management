//-----------------------------------------------------------------------
// <copyright file="KubernetesSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using FluentAssertions;
using Humanizer;
using Xunit;

#nullable enable
namespace Akka.Coordination.KubernetesApi.Tests
{
    public class KubernetesSettingsSpec
    {
        private static KubernetesSettings Conf(string? overrides)
        {
            var config = !string.IsNullOrEmpty(overrides) 
                ? ConfigurationFactory.ParseString(overrides)
                    .WithFallback(KubernetesLease.DefaultConfiguration)
                    .WithFallback(LeaseProvider.DefaultConfig())
                : KubernetesLease.DefaultConfiguration
                    .WithFallback(LeaseProvider.DefaultConfig());
            return KubernetesSettings.Create(config.GetConfig(KubernetesLease.ConfigPath), TimeoutSettings.Create(config.GetConfig("akka.coordination.lease")));
        }
        
        [Fact(DisplayName = "default request-timeout should be 2/5 of the lease-operation-timeout")]
        public void RequestTimeoutIsTwoFifthOfLeaseOperationTimeout()
        {
            Conf("akka.coordination.lease.lease-operation-timeout=10s")
                .ApiServiceRequestTimeout.Should().Be(TimeSpan.FromSeconds(4));
        }

        [Fact(DisplayName = "default body-read timeout should be 1/2 of api request timeout")]
        public void BodyReadTimeoutIsHalfOfApiRequestTimeout()
        {
            Conf("akka.coordination.lease.lease-operation-timeout=10s")
                .BodyReadTimeout.Should().Be(TimeSpan.FromSeconds(2));
        }

        [Fact(DisplayName = "Kubernetes settings should allow api server request timeout override")]
        public void ShouldAllowServerRequestTimeoutOverride()
        {
            Conf(@"
            akka.coordination.lease.lease-operation-timeout=5s
            akka.coordination.lease.kubernetes.api-service-request-timeout=4s").ApiServiceRequestTimeout
                .Should().Be(TimeSpan.FromSeconds(4));
        }

        [Fact(DisplayName =
            "Kubernetes settings should not allow server request timeout greater than operation timeout")]
        public void InvalidServerRequestTimeout()
        {
            Assert.Throws<ConfigurationException>(() =>
            {
                Conf(@"
                    akka.coordination.lease.lease-operation-timeout=5s
                    akka.coordination.lease.kubernetes.api-service-request-timeout=6s");
            }).Message.Should().Be("'api-service-request-timeout can not be less than 'lease-operation-timeout'");
        }

        [Fact(DisplayName = "KubernetesSettings should contain default values")]
        public void DefaultKubernetesSettingsTest()
        {
            var settings = Conf(null);
            settings.ApiCaPath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt");
            settings.ApiTokenPath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/token");
            settings.ApiServiceHostEnvName.Should().Be("KUBERNETES_SERVICE_HOST");
            settings.ApiServicePortEnvName.Should().Be("KUBERNETES_SERVICE_PORT");
            settings.Namespace.Should().BeNull(); 
            settings.NamespacePath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/namespace"); 
            settings.ApiServiceRequestTimeout.Should().Be(2.Seconds());
            settings.Secure.Should().BeTrue(); 
            settings.BodyReadTimeout.Should().Be(1.Seconds()); 
        }

        [Fact(DisplayName = "Empty KubernetesSettings should contain default values")]
        public void EmptyKubernetesSettingsTest()
        {
            var settings = Conf(null);
            var empty = KubernetesSettings.Empty;
            empty.ApiCaPath.Should().Be(settings.ApiCaPath);
            empty.ApiTokenPath.Should().Be(settings.ApiTokenPath);
            empty.ApiServiceHostEnvName.Should().Be(settings.ApiServiceHostEnvName);
            empty.ApiServicePortEnvName.Should().Be(settings.ApiServicePortEnvName);
            empty.Namespace.Should().Be(settings.Namespace);
            empty.NamespacePath.Should().Be(settings.NamespacePath); 
            empty.ApiServiceRequestTimeout.Should().Be(settings.ApiServiceRequestTimeout);
            empty.Secure.Should().Be(settings.Secure); 
            empty.BodyReadTimeout.Should().Be(settings.BodyReadTimeout); 
        }

        [Fact(DisplayName = "KubernetesSettings overrides should work")]
        public void KubernetesSettingsOverrideTest()
        {
            var settings = KubernetesSettings.Empty
                .WithApiCaPath("a")
                .WithApiTokenPath("b")
                .WithApiServiceHostEnvName("c")
                .WithApiServicePortEnvName("d")
                .WithNamespace("e")
                .WithNamespacePath("f")
                .WithApiServiceRequestTimeout(11.Seconds())
                .WithSecure(false)
                .WithBodyReadTimeout(12.Seconds());
            
            settings.ApiCaPath.Should().Be("a");
            settings.ApiTokenPath.Should().Be("b");
            settings.ApiServiceHostEnvName.Should().Be("c");
            settings.ApiServicePortEnvName.Should().Be("d");
            settings.Namespace.Should().Be("e"); 
            settings.NamespacePath.Should().Be("f"); 
            settings.ApiServiceRequestTimeout.Should().Be(11.Seconds());
            settings.Secure.Should().BeFalse(); 
            settings.BodyReadTimeout.Should().Be(12.Seconds()); 
        }
        
        [Fact(DisplayName = "KubernetesLeaseSetup overrides should work")]
        public void KubernetesLeaseSetupOverrideTest()
        {
            var setup = new KubernetesLeaseSetup
            {
                ApiCaPath = "a",
                ApiTokenPath = "b",
                ApiServiceHostEnvName = "c",
                ApiServicePortEnvName = "d",
                Namespace = "e",
                NamespacePath = "f",
                ApiServiceRequestTimeout = 11.Seconds(),
                Secure = false,
                BodyReadTimeout = 12.Seconds()
            };
            
            var settings = setup.Apply(KubernetesSettings.Empty);
            settings.ApiCaPath.Should().Be("a");
            settings.ApiTokenPath.Should().Be("b");
            settings.ApiServiceHostEnvName.Should().Be("c");
            settings.ApiServicePortEnvName.Should().Be("d");
            settings.Namespace.Should().Be("e"); 
            settings.NamespacePath.Should().Be("f"); 
            settings.ApiServiceRequestTimeout.Should().Be(11.Seconds());
            settings.Secure.Should().BeFalse(); 
            settings.BodyReadTimeout.Should().Be(12.Seconds()); 
        }
    }
}