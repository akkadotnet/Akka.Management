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
            return AzureLeaseSettings.Create(config, TimeoutSettings.Create(config.GetConfig(AzureLease.ConfigPath)));
        }
        
        [Fact(DisplayName = "default request-timeout should be 2/5 of the lease-operation-timeout")]
        public void RequestTimeoutIsTwoFifthOfLeaseOperationTimeout()
        {
            Assert.Equal(TimeSpan.FromSeconds(4),
                Conf($"{AzureLease.ConfigPath}.lease-operation-timeout=10s")
                    .ApiServiceRequestTimeout);
        }

        [Fact(DisplayName = "Azure settings should allow api server request timeout override")]
        public void ShouldAllowServerRequestTimeoutOverride()
        {
            Assert.Equal(TimeSpan.FromSeconds(4),
                Conf(@$"
            {AzureLease.ConfigPath}.lease-operation-timeout=5s
            {AzureLease.ConfigPath}.api-service-request-timeout=4s").ApiServiceRequestTimeout);
        }

        [Fact(DisplayName =
            "Azure settings should not allow server request timeout greater than operation timeout")]
        public void InvalidServerRequestTimeout()
        {
            var ex = Assert.Throws<ConfigurationException>(() =>
            {
                Conf(@$"
                    {AzureLease.ConfigPath}.lease-operation-timeout=5s
                    {AzureLease.ConfigPath}.api-service-request-timeout=6s");
            });
            Assert.Equal("'api-service-request-timeout can not be less than 'akka.coordination.azure.lease-operation-timeout'", ex.Message);
        }

        [Fact(DisplayName = "AzureLeaseSettings should contain default values")]
        public void DefaultAzureLeaseSettingsTest()
        {
            var settings = Conf(null);
            Assert.Equal("", settings.ConnectionString);
            Assert.Equal("akka-coordination-lease", settings.ContainerName);
            Assert.Equal(6.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.Null(settings.ServiceEndpoint);
            Assert.Null(settings.AzureCredential);
            Assert.Null(settings.BlobClientOptions);
        }

        [Fact(DisplayName = "Empty AzureLeaseSettings should contain default values")]
        public void EmptyAzureSettingsTest()
        {
            var settings = Conf(null);
            var empty = AzureLeaseSettings.Empty;
            Assert.Equal(settings.ConnectionString, empty.ConnectionString);
            Assert.Equal(settings.ContainerName, empty.ContainerName);
            Assert.Equal(settings.ApiServiceRequestTimeout, empty.ApiServiceRequestTimeout);
            Assert.Equal(settings.ServiceEndpoint, empty.ServiceEndpoint);
            Assert.Equal(settings.AzureCredential, empty.AzureCredential);
            Assert.Equal(settings.BlobClientOptions, empty.BlobClientOptions);
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
            
            Assert.Equal("a", settings.ConnectionString);
            Assert.Equal("b", settings.ContainerName);
            Assert.Equal(11.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.Equal(uri, settings.ServiceEndpoint);
            Assert.Equal(cred, settings.AzureCredential);
            Assert.Equal(opt, settings.BlobClientOptions);
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
            Assert.Equal("a", settings.ConnectionString);
            Assert.Equal("b", settings.ContainerName);
            Assert.Equal(11.Seconds(), settings.ApiServiceRequestTimeout);
            Assert.Equal(uri, settings.ServiceEndpoint);
            Assert.Equal(cred, settings.AzureCredential);
            Assert.Equal(opt, settings.BlobClientOptions);
        }
    }
}