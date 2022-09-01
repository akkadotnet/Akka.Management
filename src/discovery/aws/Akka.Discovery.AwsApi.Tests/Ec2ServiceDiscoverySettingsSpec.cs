// -----------------------------------------------------------------------
//  <copyright file="Ec2ServiceDiscoverySettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Discovery.AwsApi.Ec2;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using FluentAssertions;
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

            settings.ClientConfig.Should().BeNull();
            settings.CredentialsProvider.Should().Be("instance-metadata-credential-provider");
            settings.TagKey.Should().Be("service");
            settings.Filters.Should().BeEmpty();
            settings.Ports.Should().BeEmpty();
            settings.Endpoint.Should().BeNull();
            settings.Region.Should().BeNull();
        }

        [Fact(DisplayName = "Empty settings should be equal to default")]
        public void EmptySettingsTest()
        {
            var empty = Ec2ServiceDiscoverySettings.Empty;
            var settings = Ec2ServiceDiscoverySettings.Create(AwsEc2Discovery.DefaultConfiguration()
                .GetConfig("akka.discovery.aws-api-ec2-tag-based"));

            empty.ClientConfig.Should().Be(settings.ClientConfig);
            empty.CredentialsProvider.Should().Be(settings.CredentialsProvider);
            empty.TagKey.Should().Be(settings.TagKey);
            empty.Filters.Should().BeEquivalentTo(settings.Filters);
            empty.Ports.Should().BeEquivalentTo(settings.Ports);
            empty.Endpoint.Should().Be(settings.Endpoint);
            empty.Region.Should().Be(settings.Region);
        }

        [Fact(DisplayName = "Ec2ServiceDiscoverySettings With override should work")]
        public void SettingsWithOverrideTest()
        {
            var filters = new[] { new Filter("c", new List<string> { "d" }) }.ToImmutableList();
            var ports = new[] { 1 }.ToImmutableList();
            var settings = Ec2ServiceDiscoverySettings.Empty
                .WithClientConfig<FakeClientConfig>()
                .WithCredentialsProvider("a")
                .WithTagKey("b")
                .WithFilters(filters)
                .WithPorts(ports)
                .WithEndpoint("e")
                .WithRegion("f");
            
            settings.ClientConfig.Should().Be(typeof(FakeClientConfig));
            settings.CredentialsProvider.Should().Be("a");
            settings.TagKey.Should().Be("b");
            settings.Filters.Should().BeEquivalentTo(filters);
            settings.Ports.Should().BeEquivalentTo(ports);
            settings.Endpoint.Should().Be("e");
            settings.Region.Should().Be("f");
        }

        [Fact(DisplayName = "Ec2ServiceDiscoverySetup override should work")]
        public void SetupOverrideTest()
        {
            var filters = new[] { new Filter("b", new List<string> { "c" }) }.ToList();
            var ports = new[] { 1 }.ToList();
            var setup = new Ec2ServiceDiscoverySetup
            {
                CredentialsProvider = "a",
                TagKey = "b",
                Filters = filters,
                Ports = ports,
                Endpoint = "e",
                Region = "f"
            };
            setup.WithClientConfig<FakeClientConfig>();
            
            var settings = setup.Apply(Ec2ServiceDiscoverySettings.Empty);
            settings.ClientConfig.Should().Be(typeof(FakeClientConfig));
            settings.CredentialsProvider.Should().Be("a");
            settings.TagKey.Should().Be("b");
            settings.Filters.Should().BeEquivalentTo(filters);
            settings.Ports.Should().BeEquivalentTo(ports);
            settings.Endpoint.Should().Be("e");
            settings.Region.Should().Be("f");
        }
        
        private class FakeClientConfig: AmazonEC2Config
        {
        }
    }
}