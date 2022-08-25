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
            builder.Configuration.Value.GetConfig("akka.coordination.lease.kubernetes")
                .Should().NotBeNull();
        }
        
        [Fact(DisplayName = "Hosting Action<Setup> extension should add Setup class and default hocon settings")]
        public void HostingExtension2Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithKubernetesLease(lease =>
            {
                lease.Namespace = "underTest";
            });
                        
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig("akka.coordination.lease.kubernetes")
                .Should().NotBeNull();
            var setup = ExtractSetup(builder);
            setup.Should().NotBeNull();
            setup.Namespace.Should().Be("underTest");
        }
        
        [Fact(DisplayName = "Hosting Setup extension should add Setup class and default hocon settings")]
        public void HostingExtension3Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithKubernetesLease(new KubernetesLeaseSetup
            {
                Namespace = "underTest"
            });
                        
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig("akka.coordination.lease.kubernetes")
                .Should().NotBeNull();
            var setup = ExtractSetup(builder);
            setup.Should().NotBeNull();
            setup.Namespace.Should().Be("underTest");
        }

        private KubernetesLeaseSetup ExtractSetup(AkkaConfigurationBuilder builder)
        {
            var type = builder.GetType();
            var fieldInfo = type.GetField("Setups", BindingFlags.NonPublic | BindingFlags.Instance);
            var setups = (HashSet<Setup>) fieldInfo?.GetValue(builder);
            
            if (setups == null)
                throw new XunitException("Could not found 'Setups' field, AkkaConfigurationBuilder internal API changed");
            
            setups.Should().NotBeNull();
            return (KubernetesLeaseSetup) setups.FirstOrDefault(s => s is KubernetesLeaseSetup);
        }
    }
}