//-----------------------------------------------------------------------
// <copyright file="KubernetesSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Configuration;
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
            
            // NOTE: this is how LeaseSettings is created in Akka.Coordination
            // https://github.com/akkadotnet/akka.net/blob/f75886921174746cf80244ec18c4e61923725a2d/src/core/Akka.Coordination/LeaseProvider.cs#L127-L131
            var leaseConfig = config
                .GetConfig(KubernetesLease.ConfigPath)
                .WithFallback(config.GetConfig("akka.coordination.lease"));

            var leaseSettings =  LeaseSettings.Create(leaseConfig, "lease-name", "owner-name");
            return KubernetesSettings.Create(leaseSettings);
        }
        
        [Fact(DisplayName = "default request-timeout should be 2/5 of the lease-operation-timeout")]
        public void RequestTimeoutIsTwoFifthOfLeaseOperationTimeout()
        {
            Assert.Equal(TimeSpan.FromSeconds(4),
                Conf("akka.coordination.lease.lease-operation-timeout=10s")
                    .ApiServiceRequestTimeout);
        }

        [Fact(DisplayName = "default body-read timeout should be 1/2 of api request timeout")]
        public void BodyReadTimeoutIsHalfOfApiRequestTimeout()
        {
            Assert.Equal(TimeSpan.FromSeconds(2),
                Conf("akka.coordination.lease.lease-operation-timeout=10s")
                    .BodyReadTimeout);
        }

        [Fact(DisplayName = "Kubernetes settings should allow api server request timeout override")]
        public void ShouldAllowServerRequestTimeoutOverride()
        {
            Assert.Equal(TimeSpan.FromSeconds(4),
                Conf(@"
            akka.coordination.lease.lease-operation-timeout=5s
            akka.coordination.lease.kubernetes.api-service-request-timeout=4s").ApiServiceRequestTimeout);
        }

        [Fact(DisplayName =
            "Kubernetes settings should not allow service request timeout greater than operation timeout")]
        public void InvalidServerRequestTimeout()
        {
            var ex = Assert.Throws<ConfigurationException>(() =>
            {
                Conf(@"
                    akka.coordination.lease.lease-operation-timeout=5s
                    akka.coordination.lease.kubernetes.api-service-request-timeout=6s");
            });
            Assert.Equal("'api-service-request-timeout can not be greater than 'lease-operation-timeout'", ex.Message);
        }

        [Fact(DisplayName = "KubernetesSettings should contain default values")]
        public void DefaultKubernetesSettingsTest()
        {
            var settings = Conf(null);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt", settings.ApiCaPath);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/token", settings.ApiTokenPath);
            Assert.Equal("KUBERNETES_SERVICE_HOST", settings.ApiServiceHostEnvName);
            Assert.Equal("KUBERNETES_SERVICE_PORT", settings.ApiServicePortEnvName);
            Assert.Null(settings.Namespace);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/namespace", settings.NamespacePath);
            Assert.Equal(2.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.True(settings.Secure);
            Assert.Equal(1.Seconds(), settings.BodyReadTimeout);
        }

        [Fact(DisplayName = "Empty KubernetesSettings should contain default values")]
        public void EmptyKubernetesSettingsTest()
        {
            var settings = Conf(null);
            var empty = KubernetesSettings.Empty;
            Assert.Equal(settings.ApiCaPath, empty.ApiCaPath);
            Assert.Equal(settings.ApiTokenPath, empty.ApiTokenPath);
            Assert.Equal(settings.ApiServiceHostEnvName, empty.ApiServiceHostEnvName);
            Assert.Equal(settings.ApiServicePortEnvName, empty.ApiServicePortEnvName);
            Assert.Equal(settings.Namespace, empty.Namespace);
            Assert.Equal(settings.NamespacePath, empty.NamespacePath);
            Assert.Equal(settings.ApiServiceRequestTimeout, empty.ApiServiceRequestTimeout);
            Assert.Equal(settings.Secure, empty.Secure);
            Assert.Equal(settings.BodyReadTimeout, empty.BodyReadTimeout);
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
            
            Assert.Equal("a", settings.ApiCaPath);
            Assert.Equal("b", settings.ApiTokenPath);
            Assert.Equal("c", settings.ApiServiceHostEnvName);
            Assert.Equal("d", settings.ApiServicePortEnvName);
            Assert.Equal("e", settings.Namespace);
            Assert.Equal("f", settings.NamespacePath);
            Assert.Equal(11.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.False(settings.Secure);
            Assert.Equal(12.Seconds(), settings.BodyReadTimeout);
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
            Assert.Equal("a", settings.ApiCaPath);
            Assert.Equal("b", settings.ApiTokenPath);
            Assert.Equal("c", settings.ApiServiceHostEnvName);
            Assert.Equal("d", settings.ApiServicePortEnvName);
            Assert.Equal("e", settings.Namespace);
            Assert.Equal("f", settings.NamespacePath);
            Assert.Equal(11.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.False(settings.Secure);
            Assert.Equal(12.Seconds(), settings.BodyReadTimeout);
        }
    }
}