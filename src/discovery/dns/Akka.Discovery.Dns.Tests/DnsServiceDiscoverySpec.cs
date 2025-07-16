// -----------------------------------------------------------------------
//  <copyright file="DnsServiceDiscoverySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Dns.Tests;


public class DnsDiscoveryWithDefaultResolver(ITestOutputHelper output) : DnsServiceDiscoveryBaseSpec(
    ConfigurationFactory.ParseString(@"
                akka.loglevel = DEBUG
                akka.discovery {
                    method = akka-dns
                    akka-dns {
                        class = ""Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns""
                    }
                }
            "), output)
{
    
}
public class DnsServiceDiscoveryWithDefaultCache(ITestOutputHelper output) : DnsServiceDiscoveryBaseSpec(
    ConfigurationFactory.ParseString(@"
                akka.loglevel = DEBUG
                akka.discovery {
                    method = akka-dns
                    akka-dns {
                        class = ""Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns""
                    }
                }
                akka.io.dns.resolver = async-dns
                akka.io.dns.async-dns {
                    class = ""Akka.Discovery.Dns.Internal.DnsClient, Akka.Discovery.Dns""
                    provider-object = ""Akka.Discovery.Dns.Internal.AsyncDnsProvider, Akka.Discovery.Dns""
                    nameservers = [ 
                        ""1dot1dot1dot1.cloudflare-dns.com"", 
                        ""1.1.1.1"" ]
                    
                }
            "), output)
{
    
}


public class DnsServiceDiscoveryWithoutCache(ITestOutputHelper output) : DnsServiceDiscoveryBaseSpec(
    ConfigurationFactory.ParseString(@"
                akka.loglevel = DEBUG
                akka.discovery {
                    method = akka-dns
                    akka-dns {
                        class = ""Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns""
                    }
                }
                akka.io.dns.resolver = async-dns
                akka.io.dns.async-dns {
                    class = ""Akka.Discovery.Dns.Internal.DnsClient, Akka.Discovery.Dns""
                    provider-object = ""Akka.Discovery.Dns.Internal.AsyncDnsProvider, Akka.Discovery.Dns""
                    nameservers = [ 
                        ""1dot1dot1dot1.cloudflare-dns.com"", 
                        ""1.1.1.1"" ]
                    positive-ttl = never
                    
                }
            "), output)
{
    
}

public class DnsServiceDiscoveryWithFixedCacheTime(ITestOutputHelper output) : DnsServiceDiscoveryBaseSpec(
    ConfigurationFactory.ParseString(@"
                akka.loglevel = DEBUG
                akka.discovery {
                    method = akka-dns
                    akka-dns {
                        class = ""Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns""
                    }
                }
                akka.io.dns.resolver = async-dns
                akka.io.dns.async-dns {
                    class = ""Akka.Discovery.Dns.Internal.DnsClient, Akka.Discovery.Dns""
                    provider-object = ""Akka.Discovery.Dns.Internal.AsyncDnsProvider, Akka.Discovery.Dns""
                    nameservers = [ 
                        ""1dot1dot1dot1.cloudflare-dns.com"", 
                        ""1.1.1.1"" ]
                    positive-ttl = 10s
                    
                }
            "), output)
{
    
}
public abstract class DnsServiceDiscoveryBaseSpec(Configuration.Config config , ITestOutputHelper output) : TestKit.Xunit2.TestKit(config, "dns-discovery", output)
{
    [Fact(DisplayName = "DnsServiceDiscovery should be loadable via config")]
    public void DnsServiceDiscoveryShouldBeLoadableViaConfig()
    {
        var serviceDiscovery = Discovery.Get(Sys).LoadServiceDiscovery("akka-dns");
        serviceDiscovery.Should().BeOfType<Dns.DnsServiceDiscovery>();
    }

    [Theory(DisplayName = "DnsServiceDiscovery should handle SRV lookup with real DNS")]
    [InlineData("jabber.org", "xmpp-server", "tcp", "XMPP server")]
    [InlineData("matrix.org", "matrix", "tcp", "Matrix server")]
    [InlineData("gmail.com", "imaps", "tcp", "Gmail IMAPS")]
    public async Task DnsServiceDiscoveryShouldHandleLookup(string serviceName, string? portName, string? protocol,
        string description)
    {
        Output.WriteLine($"Testing SRV lookup for {description}: _{portName}._{protocol}.{serviceName}");
        var serviceDiscovery = new Dns.DnsServiceDiscovery((ExtendedActorSystem)Sys);

        var lookup = new Lookup(serviceName, portName, protocol);
        var resolved = await serviceDiscovery.Lookup(lookup, TimeSpan.FromSeconds(60));

        resolved.Addresses.Count.Should().BeGreaterThan(0, $"No SRV records found for {description}.");
        // Log information for diagnostic purposes
        Output.WriteLine($"Resolved targets: {resolved.Addresses.Count}");
        foreach (var addr in resolved.Addresses)
        {
            Output.WriteLine($"  Host: {addr.Host}, Address: {addr.Address}, Port: {addr.Port}");
        }

        resolved.Addresses.Count.Should().BeGreaterThan(0, "At least one SRV record should be found");
        foreach (var address in resolved.Addresses)
        {
            address.Host.Should().NotBeNullOrEmpty("Host should not be empty");
            if (serviceDiscovery.CanLookupSrv)
            {
                address.Port.Should().BeGreaterThan(0, "Port should be specified for SRV lookup");
            }
            else
            {
                address.Port.Should().BeNull( "Port should not be specified if resolver doesn't support SRV lookup");
            }
        }
    }
    
    
    [Theory(DisplayName = "DnsServiceDiscovery should handle A/AAAA lookup with real DNS")]
    [InlineData("jabber.org",  "XMPP server")]
    [InlineData("matrix.org",  "Matrix server")]
    [InlineData("gmail.com",  "Gmail IMAPS")]
    public async Task DnsServiceDiscoveryShouldHandleLookupOfA(string serviceName,
        string description)
    {
        Output.WriteLine($"Testing A/AAAA lookup for {description}: {serviceName}");
        var serviceDiscovery = new Dns.DnsServiceDiscovery((ExtendedActorSystem)Sys);

        var lookup = new Lookup(serviceName);
        var resolved = await serviceDiscovery.Lookup(lookup, TimeSpan.FromSeconds(60));

        resolved.Addresses.Count.Should().BeGreaterThan(0, $"No A/AAAA records found for {description}.");

        // Log information for diagnostic purposes
        Output.WriteLine($"Resolved targets: {resolved.Addresses.Count}");
        foreach (var addr in resolved.Addresses)
        {
            Output.WriteLine($"  Host: {addr.Host}, Address: {addr.Address}, Port: {addr.Port}");
        }

        resolved.Addresses
            .Sum(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 1 : 0)
            .Should().BeGreaterThan(0, "At least one IPv6 record should be found");
        
        resolved.Addresses
            .Sum(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 1 : 0)
            .Should().BeGreaterThan(0, "At least one IPv4 record should be found");

        
        resolved.Addresses.Count.Should().BeGreaterThan(0, "At least one SRV record should be found");
        foreach (var address in resolved.Addresses)
        {
            address.Host.Should().NotBeNullOrEmpty("Host should not be empty");
            address.Port.Should().BeNull( "Port should be specified for SRV lookup");
        }
    }
}