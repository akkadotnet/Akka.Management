// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using Akka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Akka.Coordination.Azure.Tests
{
    public class AkkaHostingSpec
    {
        [Fact(DisplayName = "Hosting extension should add default hocon settings")]
        public void HostingExtension1Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithAzureLease("");
            
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig("akka.coordination.lease.azure")
                .Should().NotBeNull();
        }
        
        [Fact(DisplayName = "Hosting Action<Setup> extension should add Setup class and default hocon settings")]
        public void HostingExtension2Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithAzureLease(lease =>
            {
                lease.ContainerName = "underTest";
            });
                        
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig("akka.coordination.lease.azure")
                .Should().NotBeNull();
            var setup = ExtractSetup(builder);
            setup.Should().NotBeNull();
            setup.ContainerName.Should().Be("underTest");
        }
        
        [Fact(DisplayName = "Hosting Setup extension should add Setup class and default hocon settings")]
        public void HostingExtension3Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithAzureLease(new AzureLeaseSetup
            {
                ContainerName = "underTest"
            });
                        
            builder.Configuration.HasValue.Should().BeTrue();
            builder.Configuration.Value.GetConfig("akka.coordination.lease.azure")
                .Should().NotBeNull();
            var setup = ExtractSetup(builder);
            setup.Should().NotBeNull();
            setup.ContainerName.Should().Be("underTest");
        }

        private static AzureLeaseSetup ExtractSetup(AkkaConfigurationBuilder builder)
        {
            return (AzureLeaseSetup) builder.Setups.FirstOrDefault(s => s is AzureLeaseSetup);
        }
    }
}