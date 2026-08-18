//-----------------------------------------------------------------------
// <copyright file="Ec2TagBasedServiceDiscoverySpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Akka.Discovery.AwsApi.Ec2;
using Xunit;

namespace Akka.Discovery.AwsApi.Tests
{
    public class Ec2TagBasedServiceDiscoverySpec
    {
        [Fact(DisplayName = "Empty string does not break parsing")]
        public void ParseEmptyString()
        {
            var result = Ec2TagBasedServiceDiscovery.ParseFiltersString("");
            Assert.Equal(0, result.Count);
        }
        
        [Fact(DisplayName = "Can parse simple filter")]
        public void ParseSimpleFilter()
        {
            var filters = "tag:purpose=demo";
            var result = Ec2TagBasedServiceDiscovery.ParseFiltersString(filters);
            Assert.Equal(1, result.Count);
            Assert.Equal("tag:purpose", result[0].Name);
            Assert.Equal(1, result[0].Values.Count);
            Assert.Equal("demo", result[0].Values[0]);
        }

        [Fact(DisplayName = "Can parse complex filter")]
        public void ParseComplexFilter()
        {
            var filters = "tag:purpose=production;tag:department=engineering;tag:critical=no;tag:numbers=one,two,three";
            var result = Ec2TagBasedServiceDiscovery.ParseFiltersString(filters);
            Assert.Equal(4, result.Count);

            Assert.Equal("tag:purpose", result[0].Name);
            // converted from BeEquivalentTo (order-insensitive)
            Assert.Equal(new List<string> {"production"}.OrderBy(x => x), result[0].Values.OrderBy(x => x));

            Assert.Equal("tag:department", result[1].Name);
            // converted from BeEquivalentTo (order-insensitive)
            Assert.Equal(new List<string> {"engineering"}.OrderBy(x => x), result[1].Values.OrderBy(x => x));

            Assert.Equal("tag:critical", result[2].Name);
            // converted from BeEquivalentTo (order-insensitive)
            Assert.Equal(new List<string> {"no"}.OrderBy(x => x), result[2].Values.OrderBy(x => x));

            Assert.Equal("tag:numbers", result[3].Name);
            // converted from BeEquivalentTo (order-insensitive)
            Assert.Equal(new List<string> {"one", "two", "three"}.OrderBy(x => x), result[3].Values.OrderBy(x => x));
        }
    }
}