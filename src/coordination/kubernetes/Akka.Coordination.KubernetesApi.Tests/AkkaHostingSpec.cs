// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Hosting;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Akka.Coordination.KubernetesApi.Tests
{
    public class AkkaHostingSpec
    {
        [Fact(DisplayName = "Hosting extension should add default hocon settings")]
        public void HostingExtension1Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithKubernetesLease();
            
            Assert.True(builder.Configuration.HasValue);
            Assert.NotNull(builder.Configuration.Value.GetConfig(KubernetesLease.ConfigPath));
            
            var leaseSettings = GetSettings(builder);
            var settings = KubernetesSettings.Create(leaseSettings);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt", settings.ApiCaPath);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/token", settings.ApiTokenPath);
            Assert.Equal("KUBERNETES_SERVICE_HOST", settings.ApiServiceHostEnvName);
            Assert.Equal("KUBERNETES_SERVICE_PORT", settings.ApiServicePortEnvName);
            Assert.Null(settings.Namespace);
            Assert.Equal("/var/run/secrets/kubernetes.io/serviceaccount/namespace", settings.NamespacePath);
            Assert.Equal(2.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.True(settings.Secure);
            Assert.Equal(1.Seconds(), settings.BodyReadTimeout);

            var timeSettings = TimeoutSettings.Create(leaseSettings.LeaseConfig);
            Assert.Equal(12.Seconds(), timeSettings.HeartbeatInterval);
            Assert.Equal(120.Seconds(), timeSettings.HeartbeatTimeout);
            Assert.Equal(5.Seconds(), timeSettings.OperationTimeout);
        }
        
        [Fact(DisplayName = "Hosting Action<KubernetesLeaseOption> extension should override hocon settings")]
        public void HostingExtension2Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithKubernetesLease(lease =>
            {
                lease.ApiCaPath = "a";
                lease.ApiTokenPath = "b";
                lease.ApiServiceHostEnvName = "c";
                lease.ApiServicePortEnvName = "d";
                lease.Namespace = "e";
                lease.NamespacePath = "f";
                lease.ApiServiceRequestTimeout = 3.Seconds();
                lease.SecureApiServer = false;
                lease.HeartbeatInterval = 4.Seconds();
                lease.HeartbeatTimeout = 10.Seconds();
                lease.LeaseOperationTimeout = 4.Seconds();
            });
                        
            Assert.True(builder.Configuration.HasValue);
            Assert.NotNull(builder.Configuration.Value.GetConfig(KubernetesLease.ConfigPath));
            
            var leaseSettings = GetSettings(builder);
            var settings = KubernetesSettings.Create(leaseSettings);
            Assert.Equal("a", settings.ApiCaPath);
            Assert.Equal("b", settings.ApiTokenPath);
            Assert.Equal("c", settings.ApiServiceHostEnvName);
            Assert.Equal("d", settings.ApiServicePortEnvName);
            Assert.Equal("e", settings.Namespace);
            Assert.Equal("f", settings.NamespacePath);
            Assert.Equal(3.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.False(settings.Secure);
            Assert.Equal(1.5.Seconds(), settings.BodyReadTimeout);

            var timeSettings = TimeoutSettings.Create(leaseSettings.LeaseConfig);
            Assert.Equal(4.Seconds(), timeSettings.HeartbeatInterval);
            Assert.Equal(10.Seconds(), timeSettings.HeartbeatTimeout);
            Assert.Equal(4.Seconds(), timeSettings.OperationTimeout);
        }
        
        [Fact(DisplayName = "Hosting Setup extension should override hocon settings")]
        public void HostingExtension3Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithKubernetesLease(new KubernetesLeaseOption
            {
                ApiCaPath = "a",
                ApiTokenPath = "b",
                ApiServiceHostEnvName = "c",
                ApiServicePortEnvName = "d",
                Namespace = "e",
                NamespacePath = "f",
                ApiServiceRequestTimeout = 3.Seconds(),
                SecureApiServer = false,
                HeartbeatInterval = 4.Seconds(),
                HeartbeatTimeout = 10.Seconds(),
                LeaseOperationTimeout = 4.Seconds()
            });
                        
            Assert.True(builder.Configuration.HasValue);
            Assert.NotNull(builder.Configuration.Value.GetConfig(KubernetesLease.ConfigPath));
            
            var leaseSettings = GetSettings(builder);
            var settings = KubernetesSettings.Create(leaseSettings);
            Assert.Equal("a", settings.ApiCaPath);
            Assert.Equal("b", settings.ApiTokenPath);
            Assert.Equal("c", settings.ApiServiceHostEnvName);
            Assert.Equal("d", settings.ApiServicePortEnvName);
            Assert.Equal("e", settings.Namespace);
            Assert.Equal("f", settings.NamespacePath);
            Assert.Equal(3.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.False(settings.Secure);
            Assert.Equal(1.5.Seconds(), settings.BodyReadTimeout);

            var timeSettings = TimeoutSettings.Create(leaseSettings.LeaseConfig);
            Assert.Equal(4.Seconds(), timeSettings.HeartbeatInterval);
            Assert.Equal(10.Seconds(), timeSettings.HeartbeatTimeout);
            Assert.Equal(4.Seconds(), timeSettings.OperationTimeout);
        }

        private static LeaseSettings GetSettings(AkkaConfigurationBuilder builder)
        {
            // NOTE: this is how LeaseSettings is created in Akka.Coordination
            // https://github.com/akkadotnet/akka.net/blob/f75886921174746cf80244ec18c4e61923725a2d/src/core/Akka.Coordination/LeaseProvider.cs#L127-L131
            var leaseConfig = builder.Configuration.Value
                .GetConfig(KubernetesLease.ConfigPath)
                .WithFallback(builder.Configuration.Value.GetConfig("akka.coordination.lease"));

            return LeaseSettings.Create(leaseConfig, "lease-name", "owner-name");
        }
    }
}