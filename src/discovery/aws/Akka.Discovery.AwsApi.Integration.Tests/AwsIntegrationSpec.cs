//-----------------------------------------------------------------------
// <copyright file="AwsIntegrationSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

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
  remote.dot-netty.tcp.port = 0
  discovery {{
    method = ""aws-api-ec2-tag-based""
    aws-api-ec2-tag-based {{
      client-config = """"
      credentials-provider = ""{typeof(AnonymousEc2CredentialProvider).AssemblyQualifiedName}""
      class = ""{typeof(Ec2TagBasedServiceDiscovery).AssemblyQualifiedName}""
      tag-key = ""service""
      filters = """"
      ports = []
      endpoint = ""{fixture.Endpoint}""
      # region = """"
      anonymous-credential-provider {{
        # Fully qualified class name of a class that extends Akka.Discovery.AwsApi.Ec2.Ec2ConfigurationProvider with either 
        # a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
        class = ""Akka.Discovery.AwsApi.Ec2.AnonymousEc2CredentialProvider, Akka.Discovery.AwsApi""    
      }}
    }}
  }} 
}}"); 
        private readonly LocalStackFixture _fixture;
        
        public AwsIntegrationSpec(ITestOutputHelper output, LocalStackFixture fixture) 
            : base(Config(fixture), nameof(AwsIntegrationSpec), output)
        {
            _fixture = fixture;
        }
        
        [SkippableFact]
        public async Task DiscoveryShouldBeAbleToLookupAwsEc2Instances()
        {
            Skip.If(_fixture.IsWindows, "LocalStack docker image only available for Linux OS");
            
            var discovery = new Ec2TagBasedServiceDiscovery((ExtendedActorSystem)Sys);
            var lookup = new Lookup("fake-api");
            var resolved = await discovery.Lookup(lookup, TimeSpan.FromSeconds(5));
            resolved.Addresses.Count.Should().Be(4);
            resolved.Addresses.Select(a => a.Address.ToString()).Should().BeEquivalentTo(_fixture.IpAddresses);
        }

    }
}