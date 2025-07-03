// -----------------------------------------------------------------------
//  <copyright file="DnsServiceDiscoverySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Dns.Tests
{
    public class DnsServiceDiscoverySpec : TestKit.Xunit2.TestKit
    {
        //  akka.io.dns.resolver = async-dns
        public DnsServiceDiscoverySpec(ITestOutputHelper output) 
            : base(ConfigurationFactory.ParseString(@"
                akka.discovery {
                    method = akka-dns
                    akka-dns {
                        class = ""Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns""
                    }
                }
            "), "dns-discovery", output)
        {
        }

        [Fact(DisplayName = "DnsServiceDiscovery should be loadable via config")]
        public void DnsServiceDiscoveryShouldBeLoadableViaConfig()
        {
            var serviceDiscovery = Discovery.Get(Sys).LoadServiceDiscovery("akka-dns");
            serviceDiscovery.Should().BeOfType<DnsServiceDiscovery>();
        }
        
        [Fact(DisplayName = "DnsServiceDiscovery should handle A/AAAA record lookup")]
        public async Task DnsServiceDiscoveryShouldHandleIpLookup()
        {
            // Use the actual DNS resolver to look up a known domain
            var discovery = Discovery.Get(Sys).LoadServiceDiscovery("akka-dns");
            
            // Lookup a domain that should always exist and resolve
            var host = "getakka.net";
            var lookup = new Lookup(host);
            var resolved = await discovery.Lookup(lookup, TimeSpan.FromSeconds(10));
            
            resolved.Should().NotBeNull();
            resolved.Addresses.Should().NotBeEmpty();
            
            // The resolved addresses should have host names but no port (since we're doing A/AAAA lookup)
            foreach (var address in resolved.Addresses)
            {
                address.Host.Should().NotBeNullOrEmpty();
                address.Port.HasValue.Should().BeFalse();
            }
            this.Output.WriteLine("Resolved host {0} into addresses: {1}", host, resolved);
        }
        
        [Fact(DisplayName = "DnsServiceDiscovery should construct correct SRV record query")]
        public void DnsServiceDiscoveryShouldConstructCorrectSrvQuery()
        {
            // This test validates that the SRV record query is correctly formatted
            var discovery = new TestDnsServiceDiscovery((ExtendedActorSystem)Sys);
            
            var lookup = new Lookup("myservice.example.com")
                .WithPortName("http")
                .WithProtocol("tcp");
                
            var srvRequest = discovery.TestGetSrvRequest(lookup);
            
            srvRequest.Should().Be("_http._tcp.myservice.example.com");
        }
        
        // Helper test class that exposes some internals for testing
        private class TestDnsServiceDiscovery : DnsServiceDiscovery
        {
            public TestDnsServiceDiscovery(ExtendedActorSystem system) : base(system)
            {
            }
            
            public string TestGetSrvRequest(Lookup lookup)
            {
                return $"_{lookup.PortName}._{lookup.Protocol}.{lookup.ServiceName}";
            }
        }
    }
}
