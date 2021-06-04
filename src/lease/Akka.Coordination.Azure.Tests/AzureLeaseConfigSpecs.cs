using System;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Xunit;

namespace Akka.Coordination.Azure.Tests
{
    public class AzureLeaseConfigSpecs
    {
        [Fact]
        public void AzureLeaseConfigShouldLoadDefaultSettings()
        {
            var settings = AzureLeaseConfig.Create(AzureLeaseHelpers.DefaultConfig.GetConfig("akka.coordination.lease.azure"));

            settings.ConnectionString.Should().BeNullOrEmpty();
            settings.ContainerName.Should().Be("akkadotnet-lease-container");

            settings.AutoInitialize.Should().BeTrue();
            settings.ContainerPublicAccessType.Should().Be(PublicAccessType.None);
        }
    }
}
