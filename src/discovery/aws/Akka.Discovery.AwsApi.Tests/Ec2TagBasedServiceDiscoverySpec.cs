using System.Collections.Generic;
using Akka.Discovery.AwsApi.Ec2;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.AwsApi.Tests
{
    public class Ec2TagBasedServiceDiscoverySpec
    {
        [Fact(DisplayName = "Empty string does not break parsing")]
        public void ParseEmptyString()
        {
            var result = Ec2TagBasedServiceDiscovery.ParseFiltersString("");
            result.Count.Should().Be(0);
        }
        
        [Fact(DisplayName = "Can parse simple filter")]
        public void ParseSimpleFilter()
        {
            var filters = "tag:purpose=demo";
            var result = Ec2TagBasedServiceDiscovery.ParseFiltersString(filters);
            result.Count.Should().Be(1);
            result[0].Name.Should().Be("tag:purpose");
            result[0].Values.Count.Should().Be(1);
            result[0].Values[0].Should().Be("demo");
        }

        [Fact(DisplayName = "Can parse complex filter")]
        public void ParseComplexFilter()
        {
            var filters = "tag:purpose=production;tag:department=engineering;tag:critical=no;tag:numbers=one,two,three";
            var result = Ec2TagBasedServiceDiscovery.ParseFiltersString(filters);
            result.Count.Should().Be(4);

            result[0].Name.Should().Be("tag:purpose");
            result[0].Values.Should().BeEquivalentTo(new List<string> {"production"});
            
            result[1].Name.Should().Be("tag:department");
            result[1].Values.Should().BeEquivalentTo(new List<string> {"engineering"});
            
            result[2].Name.Should().Be("tag:critical");
            result[2].Values.Should().BeEquivalentTo(new List<string> {"no"});
            
            result[3].Name.Should().Be("tag:numbers");
            result[3].Values.Should().BeEquivalentTo(new List<string> {"one", "two", "three"});
        }
    }
}