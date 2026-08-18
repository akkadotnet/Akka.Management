// -----------------------------------------------------------------------
//  <copyright file="Ec2ServiceDiscoverySettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.AwsApi.Ec2;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Xunit;

namespace Akka.Discovery.AwsApi.Tests
{
    public class Ec2ServiceDiscoverySettingsSpec
    {
        [Fact(DisplayName = "Default settings should contain default values")]
        public void DefaultSettingsTest()
        {
            var settings = Ec2ServiceDiscoverySettings.Create(
                AwsEc2Discovery.DefaultConfiguration().GetConfig("akka.discovery.aws-api-ec2-tag-based"));

            Assert.Null(settings.ClientConfig);
            Assert.Equal(typeof(Ec2InstanceMetadataCredentialProvider), settings.CredentialsProvider);
            Assert.Equal("service", settings.TagKey);
            Assert.Empty(settings.Filters);
            Assert.Empty(settings.Ports);
            Assert.Null(settings.Endpoint);
            Assert.Null(settings.Region);
        }

        [Fact(DisplayName = "Empty settings should be equal to default")]
        public void EmptySettingsTest()
        {
            var empty = Ec2ServiceDiscoverySettings.Empty;
            var settings = Ec2ServiceDiscoverySettings.Create(AwsEc2Discovery.DefaultConfiguration()
                .GetConfig("akka.discovery.aws-api-ec2-tag-based"));

            Assert.Equal(settings.ClientConfig, empty.ClientConfig);
            Assert.Equal(settings.CredentialsProvider, empty.CredentialsProvider);
            Assert.Equal(settings.TagKey, empty.TagKey);
            // converted from BeEquivalentTo (structural, order-insensitive)
            Assert.Equal(
                settings.Filters.Select(f => (f.Name, Values: string.Join(",", f.Values.OrderBy(v => v)))).OrderBy(t => t.Name),
                empty.Filters.Select(f => (f.Name, Values: string.Join(",", f.Values.OrderBy(v => v)))).OrderBy(t => t.Name));
            // converted from BeEquivalentTo (order-insensitive)
            Assert.Equal(settings.Ports.OrderBy(p => p), empty.Ports.OrderBy(p => p));
            Assert.Equal(settings.Endpoint, empty.Endpoint);
            Assert.Equal(settings.Region, empty.Region);
        }

        [Fact(DisplayName = "Ec2ServiceDiscoverySettings With override should work")]
        public void SettingsWithOverrideTest()
        {
            var filters = new[] { new Filter("c", new List<string> { "d" }) }.ToImmutableList();
            var ports = new[] { 1 }.ToImmutableList();
            var settings = Ec2ServiceDiscoverySettings.Empty
                .WithClientConfig<FakeClientConfig>()
                .WithCredentialsProvider<FakeCredProvider>()
                .WithTagKey("b")
                .WithFilters(filters)
                .WithPorts(ports)
                .WithEndpoint("e")
                .WithRegion("f");
            
            Assert.Equal(typeof(FakeClientConfig), settings.ClientConfig);
            Assert.Equal(typeof(FakeCredProvider), settings.CredentialsProvider);
            Assert.Equal("b", settings.TagKey);
            // converted from BeEquivalentTo (structural, order-insensitive)
            Assert.Equal(
                filters.Select(f => (f.Name, Values: string.Join(",", f.Values.OrderBy(v => v)))).OrderBy(t => t.Name),
                settings.Filters.Select(f => (f.Name, Values: string.Join(",", f.Values.OrderBy(v => v)))).OrderBy(t => t.Name));
            // converted from BeEquivalentTo (order-insensitive)
            Assert.Equal(ports.OrderBy(p => p), settings.Ports.OrderBy(p => p));
            Assert.Equal("e", settings.Endpoint);
            Assert.Equal("f", settings.Region);
        }

        [Fact(DisplayName = "Ec2ServiceDiscoverySetup override should work")]
        public void SetupOverrideTest()
        {
            var filters = new[] { new Filter("b", new List<string> { "c" }) }.ToList();
            var ports = new[] { 1 }.ToList();
            var setup = new Ec2ServiceDiscoverySetup
            {
                TagKey = "b",
                Filters = filters,
                Ports = ports,
                Endpoint = "e",
                Region = "f"
            }
                .WithClientConfig<FakeClientConfig>()
                .WithCredentialProvider<FakeCredProvider>();
            
            var settings = setup.Apply(Ec2ServiceDiscoverySettings.Empty);
            Assert.Equal(typeof(FakeClientConfig), settings.ClientConfig);
            Assert.Equal(typeof(FakeCredProvider), settings.CredentialsProvider);
            Assert.Equal("b", settings.TagKey);
            // converted from BeEquivalentTo (structural, order-insensitive)
            Assert.Equal(
                filters.Select(f => (f.Name, Values: string.Join(",", f.Values.OrderBy(v => v)))).OrderBy(t => t.Name),
                settings.Filters.Select(f => (f.Name, Values: string.Join(",", f.Values.OrderBy(v => v)))).OrderBy(t => t.Name));
            // converted from BeEquivalentTo (order-insensitive)
            Assert.Equal(ports.OrderBy(p => p), settings.Ports.OrderBy(p => p));
            Assert.Equal("e", settings.Endpoint);
            Assert.Equal("f", settings.Region);
        }

        [Fact(DisplayName = "Ec2ServiceDiscoverySetup Type based properties should validate values")]
        public void StrictTypePropertyTest()
        {
            var setup = new Ec2ServiceDiscoverySetup();

            Assert.Null(Record.Exception(() => { setup.ClientConfig = typeof(FakeClientConfig); }));
            Assert.Null(Record.Exception(() => { setup.ClientConfig = typeof(FakeClientConfig2); }));
            // converted from ThrowExactly + WithMessage glob "*Type value need to extend*"
            Assert.Contains("Type value need to extend",
                Assert.Throws<ConfigurationException>(() => { setup.ClientConfig = typeof(FakeCredProvider); }).Message);
            // converted from ThrowExactly + WithMessage glob "*need to have a parameterless constructor*"
            Assert.Contains("need to have a parameterless constructor",
                Assert.Throws<ConfigurationException>(() => { setup.ClientConfig = typeof(IllegalClientConfig); }).Message);

            Assert.Null(Record.Exception(() => { setup.WithClientConfig<FakeClientConfig>(); }));
            Assert.Null(Record.Exception(() => { setup.WithClientConfig<FakeClientConfig2>(); }));
            Assert.Contains("need to have a parameterless constructor",
                Assert.Throws<ConfigurationException>(() => { setup.WithClientConfig<IllegalClientConfig>(); }).Message);

            Assert.Null(Record.Exception(() => { setup.CredentialsProvider = typeof(FakeCredProvider); }));
            Assert.Null(Record.Exception(() => { setup.CredentialsProvider = typeof(FakeCredProvider2); }));
            Assert.Contains("Type value need to extend",
                Assert.Throws<ConfigurationException>(() => { setup.CredentialsProvider = typeof(FakeClientConfig); }).Message);
            Assert.Contains("need to have a parameterless constructor",
                Assert.Throws<ConfigurationException>(() => { setup.CredentialsProvider = typeof(IllegalCredProvider); }).Message);

            Assert.Null(Record.Exception(() => { setup.WithCredentialProvider<FakeCredProvider>(); }));
            Assert.Null(Record.Exception(() => { setup.WithCredentialProvider<FakeCredProvider2>(); }));
            Assert.Contains("need to have a parameterless constructor",
                Assert.Throws<ConfigurationException>(() => { setup.WithCredentialProvider<IllegalCredProvider>(); }).Message);
        }
        
        private class FakeClientConfig: AmazonEC2Config
        {
        }

        private class FakeClientConfig2: AmazonEC2Config
        {
            public FakeClientConfig2(ExtendedActorSystem system) { }
        }
        
        private class IllegalClientConfig: AmazonEC2Config
        {
            public IllegalClientConfig(string wrongParam) { }
        }
        
        private class FakeCredProvider: Ec2CredentialProvider
        {
            public override AWSCredentials ClientCredentials => new AnonymousAWSCredentials();
        }
        
        private class FakeCredProvider2: Ec2CredentialProvider
        {
            public FakeCredProvider2(ExtendedActorSystem system) { }
            
            public override AWSCredentials ClientCredentials => new AnonymousAWSCredentials();
        }
        
        private class IllegalCredProvider: Ec2CredentialProvider
        {
            public IllegalCredProvider(string wrongParam) { }
            
            public override AWSCredentials ClientCredentials => new AnonymousAWSCredentials();
        }
    }
}