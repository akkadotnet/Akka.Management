// -----------------------------------------------------------------------
//  <copyright file="AkkaHostingSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using Akka.Hosting;
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
            
            Assert.True(builder.Configuration.HasValue);
            Assert.NotNull(builder.Configuration.Value.GetConfig("akka.coordination.lease.azure"));
        }
        
        [Fact(DisplayName = "Hosting Action<Options> extension should add default hocon settings")]
        public void HostingExtension2Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithAzureLease(lease =>
            {
                lease.ContainerName = "underTest";
            });
                        
            Assert.True(builder.Configuration.HasValue);
            var config = builder.Configuration.Value.GetConfig("akka.coordination.lease.azure");
            Assert.NotNull(config);
            Assert.Equal("underTest", config.GetString("container-name"));
        }
        
        [Fact(DisplayName = "Hosting options extension should add default hocon settings")]
        public void HostingExtension3Test()
        {
            var builder = new AkkaConfigurationBuilder(new ServiceCollection(), "test");
            
            builder.WithAzureLease(new AzureLeaseOption
            {
                ContainerName = "underTest"
            });
                        
            Assert.True(builder.Configuration.HasValue);
            var config = builder.Configuration.Value.GetConfig("akka.coordination.lease.azure");
            Assert.NotNull(config);
            Assert.Equal("underTest", config.GetString("container-name"));
        }
    }
}