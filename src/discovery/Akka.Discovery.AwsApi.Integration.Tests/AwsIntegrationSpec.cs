using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.AwsApi.Ec2;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.AwsApi.Integration.Tests
{
    [Collection("AwsSpec")]
    public class AwsIntegrationSpec : TestKit.Xunit2.TestKit
    {
        private static Configuration.Config Config(LocalStackFixture fixture) => ConfigurationFactory.ParseString($@"
akka {{
  actor.provider = ""cluster""
  discovery {{
    method = ""aws-api-ec2-tag-based""
    aws-api-ec2-tag-based {{
      client-config = """"
      class = ""Akka.Discovery.AwsApi.Ec2.Ec2TagBasedServiceDiscovery, Akka.Discovery.AwsApi""
      tag-key = ""service""
      filters = """"
      ports = []
      endpoint = ""{fixture.Endpoint}""
      # region = """"
    }}
  }} 
}}"); 
        private readonly LocalStackFixture _fixture;
        
        public AwsIntegrationSpec(ITestOutputHelper output, LocalStackFixture fixture) 
            : base(Config(fixture), nameof(AwsIntegrationSpec), output)
        {
            _fixture = fixture;
        }
        
        [Fact]
        public async Task DiscoveryShouldBeAbleToLookupAwsEc2Instances()
        {
            var discovery = new Ec2TagBasedServiceDiscovery((ExtendedActorSystem)Sys);
            var lookup = new Lookup("fake-api");
            var resolved = await discovery.Lookup(lookup, TimeSpan.FromSeconds(5));
            resolved.Addresses.Count.Should().Be(4);
            resolved.Addresses.Select(a => a.Address.ToString()).Should().BeEquivalentTo(_fixture.IpAddresses);
        }

    }
}