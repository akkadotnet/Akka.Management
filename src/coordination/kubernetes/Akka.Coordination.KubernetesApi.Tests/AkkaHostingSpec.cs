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
using FluentAssertions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Sdk;

namespace Akka.Coordination.KubernetesApi.Tests
{
    public class AkkaHostingSpec
    {
        [Fact(DisplayName = "Hosting extension should add default hocon settings")]
        public void HostingExtension1Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithKubernetesLease();
            
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig(KubernetesLease.ConfigPath).Should().NotBeNull();
            
            var leaseSettings = GetSettings(builder);
            var settings = KubernetesSettings.Create(leaseSettings);
            settings.ApiCaPath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/ca.crt");
            settings.ApiTokenPath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/token");
            settings.ApiServiceHostEnvName.Should().Be("KUBERNETES_SERVICE_HOST");
            settings.ApiServicePortEnvName.Should().Be("KUBERNETES_SERVICE_PORT");
            settings.Namespace.Should().BeNull(); 
            settings.NamespacePath.Should().Be("/var/run/secrets/kubernetes.io/serviceaccount/namespace"); 
            settings.ApiServiceRequestTimeout.Should().Be(2.Seconds());
            settings.Secure.Should().BeTrue(); 
            settings.BodyReadTimeout.Should().Be(1.Seconds()); 

            var timeSettings = TimeoutSettings.Create(leaseSettings.LeaseConfig);
            timeSettings.HeartbeatInterval.Should().Be(12.Seconds());
            timeSettings.HeartbeatTimeout.Should().Be(120.Seconds());
            timeSettings.OperationTimeout.Should().Be(5.Seconds());
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
                        
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig(KubernetesLease.ConfigPath).Should().NotBeNull();
            
            var leaseSettings = GetSettings(builder);
            var settings = KubernetesSettings.Create(leaseSettings);
            settings.ApiCaPath.Should().Be("a");
            settings.ApiTokenPath.Should().Be("b");
            settings.ApiServiceHostEnvName.Should().Be("c");
            settings.ApiServicePortEnvName.Should().Be("d");
            settings.Namespace.Should().Be("e"); 
            settings.NamespacePath.Should().Be("f"); 
            settings.ApiServiceRequestTimeout.Should().Be(3.Seconds());
            settings.Secure.Should().BeFalse(); 
            settings.BodyReadTimeout.Should().Be(1.5.Seconds());

            var timeSettings = TimeoutSettings.Create(leaseSettings.LeaseConfig);
            timeSettings.HeartbeatInterval.Should().Be(4.Seconds());
            timeSettings.HeartbeatTimeout.Should().Be(10.Seconds());
            timeSettings.OperationTimeout.Should().Be(4.Seconds());
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
                        
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig(KubernetesLease.ConfigPath).Should().NotBeNull();
            
            var leaseSettings = GetSettings(builder);
            var settings = KubernetesSettings.Create(leaseSettings);
            settings.ApiCaPath.Should().Be("a");
            settings.ApiTokenPath.Should().Be("b");
            settings.ApiServiceHostEnvName.Should().Be("c");
            settings.ApiServicePortEnvName.Should().Be("d");
            settings.Namespace.Should().Be("e"); 
            settings.NamespacePath.Should().Be("f"); 
            settings.ApiServiceRequestTimeout.Should().Be(3.Seconds());
            settings.Secure.Should().BeFalse(); 
            settings.BodyReadTimeout.Should().Be(1.5.Seconds());

            var timeSettings = TimeoutSettings.Create(leaseSettings.LeaseConfig);
            timeSettings.HeartbeatInterval.Should().Be(4.Seconds());
            timeSettings.HeartbeatTimeout.Should().Be(10.Seconds());
            timeSettings.OperationTimeout.Should().Be(4.Seconds());
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