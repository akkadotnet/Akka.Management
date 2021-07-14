using System;
using System.Collections.Generic;
using System.IO;
using Akka.Configuration;
using Akka.Event;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.EC2;
using Amazon.Runtime;
using Amazon.S3;
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
        private readonly ServiceDiscovery _discovery;

        private const string StackName = "AkkaManagementIntegrationTestEC2TagBased";
        private const int InstanceCount = 3;
        
        private readonly List<string> _clusterPublicIps = new List<string>();
        private readonly List<string> _clusterPrivateIps = new List<string>();
        
        public AwsIntegrationSpec(ITestOutputHelper output, LocalStackFixture fixture) 
            : base(Config(fixture), nameof(AwsIntegrationSpec), output)
        {
            _fixture = fixture;
            _discovery = Discovery.Get(Sys).LoadServiceDiscovery("Ec2TagBasedServiceDiscovery");

            
/*
            string template;
            using(Stream stream = GetType().Assembly.GetManifestResourceStream("akka-cluster.json"))
            {
                using (var reader = new StreamReader(stream))
                {
                    template = reader.ReadToEnd();
                }
            }

            var createStackRequest = new CreateStackRequest
            {
                Capabilities = new List<string> {"CAPABILITY_IAM"},
                StackName = StackName,
                TemplateBody = template,
                Parameters = new List<Parameter>
                {
                    new Parameter()
                }
            };
*/
        }
        
        [Fact]
        public void Test1()
        {
            var client = _fixture.S3Client;
        }
    }
}