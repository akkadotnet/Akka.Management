//-----------------------------------------------------------------------
// <copyright file="AzureLeaseSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

#nullable enable
using System;
using Akka.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using FluentAssertions;
using Humanizer;
using Xunit;

namespace Akka.Coordination.Azure.Tests
{
    public class AzureLeaseSettingsSpec
    {
        private static AzureLeaseSettings Conf(string? overrides)
        {
            var config = !string.IsNullOrEmpty(overrides) 
                ? ConfigurationFactory.ParseString(overrides)
                    .WithFallback(AzureLease.DefaultConfiguration)
                    .WithFallback(LeaseProvider.DefaultConfig())
                : AzureLease.DefaultConfiguration
                    .WithFallback(LeaseProvider.DefaultConfig());
            return AzureLeaseSettings.Create(config, TimeoutSettings.Create(config.GetConfig("akka.coordination.lease")));
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

        [Fact(DisplayName = "Azure settings should allow api server request timeout override")]
        public void ShouldAllowServerRequestTimeoutOverride()
        {
            Conf(@"
            akka.coordination.lease.lease-operation-timeout=5s
            akka.coordination.lease.azure.api-service-request-timeout=4s").ApiServiceRequestTimeout
                .Should().Be(TimeSpan.FromSeconds(4));
        }

        [Fact(DisplayName =
            "Azure settings should not allow server request timeout greater than operation timeout")]
        public void InvalidServerRequestTimeout()
        {
            Assert.Throws<ConfigurationException>(() =>
            {
                Conf(@"
                    akka.coordination.lease.lease-operation-timeout=5s
                    akka.coordination.lease.azure.api-service-request-timeout=6s");
            }).Message.Should().Be("'api-service-request-timeout can not be less than 'akka.coordination.lease.lease-operation-timeout'");
        }

        [Fact(DisplayName = "AzureLeaseSettings should contain default values")]
        public void DefaultAzureLeaseSettingsTest()
        {
            var settings = Conf(null);
            settings.ConnectionString.Should().Be("");
            settings.ContainerName.Should().Be("akka-coordination-lease");
            settings.ApiServiceRequestTimeout.Should().Be(2.Seconds());
            settings.BodyReadTimeout.Should().Be(1.Seconds());
            settings.ServiceEndpoint.Should().BeNull();
            settings.AzureCredential.Should().BeNull();
            settings.BlobClientOptions.Should().BeNull();
        }

        [Fact(DisplayName = "Empty AzureLeaseSettings should contain default values")]
        public void EmptyAzureSettingsTest()
        {
            var settings = Conf(null);
            var empty = AzureLeaseSettings.Empty;
            empty.ConnectionString.Should().Be(settings.ConnectionString);
            empty.ContainerName.Should().Be(settings.ContainerName);
            empty.ApiServiceRequestTimeout.Should().Be(settings.ApiServiceRequestTimeout);
            empty.BodyReadTimeout.Should().Be(settings.BodyReadTimeout);
            empty.ServiceEndpoint.Should().Be(settings.ServiceEndpoint);
            empty.AzureCredential.Should().Be(settings.AzureCredential); 
            empty.BlobClientOptions.Should().Be(settings.BlobClientOptions);
        }

        [Fact(DisplayName = "AzureLeaseSettings overrides should work")]
        public void AzureSettingsOverrideTest()
        {
            var uri = new Uri("http://whatever:80");
            var cred = new DefaultAzureCredential();
            var opt = new BlobClientOptions();
            
            var settings = AzureLeaseSettings.Empty
                .WithConnectionString("a")
                .WithContainerName("b")
                .WithApiServiceRequestTimeout(11.Seconds())
                .WithAzureCredential(cred, uri)
                .WithBlobClientOption(opt);
            
            settings.ConnectionString.Should().Be("a");
            settings.ContainerName.Should().Be("b");
            settings.ApiServiceRequestTimeout.Should().Be(11.Seconds());
            settings.BodyReadTimeout.Should().Be(5.5.Seconds()); 
            settings.ServiceEndpoint.Should().Be(uri);
            settings.AzureCredential.Should().Be(cred); 
            settings.BlobClientOptions.Should().Be(opt);
        }
        
        [Fact(DisplayName = "AzureLeaseSetup overrides should work")]
        public void AzureLeaseSetupOverrideTest()
        {
            var uri = new Uri("http://whatever:80");
            var cred = new DefaultAzureCredential();
            var opt = new BlobClientOptions();
            
            var setup = new AzureLeaseSetup
            {
                ConnectionString = "a",
                ContainerName = "b",
                ApiServiceRequestTimeout = 11.Seconds(),
                ServiceEndpoint = uri,
                AzureCredential = cred,
                BlobClientOptions = opt
            };
            
            var settings = setup.Apply(AzureLeaseSettings.Empty, null!);
            settings.ConnectionString.Should().Be("a");
            settings.ContainerName.Should().Be("b");
            settings.ApiServiceRequestTimeout.Should().Be(11.Seconds());
            settings.BodyReadTimeout.Should().Be(5.5.Seconds()); 
            settings.ServiceEndpoint.Should().Be(uri);
            settings.AzureCredential.Should().Be(cred); 
            settings.BlobClientOptions.Should().Be(opt);
        }
    }
}