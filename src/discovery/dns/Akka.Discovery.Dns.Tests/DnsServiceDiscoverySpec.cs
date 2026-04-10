// -----------------------------------------------------------------------
//  <copyright file="DnsServiceDiscoverySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Dns.Internal;
using Akka.IO;
using FluentAssertions;
using Xunit;

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
            "), "DnsDiscoveryWithDefaultResolver", output)
{
    // skip check of AAAA records on Windows with default resolver
    internal override bool DoNotExpectAAAARecordsFromInetResolver { get; }
        = Environment.OSVersion.Platform != PlatformID.Unix;
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
                    provider-object = ""Akka.Discovery.Dns.Internal.AsyncDnsProvider, Akka.Discovery.Dns""
                    nameservers = [ 
                        ""1dot1dot1dot1.cloudflare-dns.com"", 
                        ""1.1.1.1"" ]
                    
                }
            "), "DnsServiceDiscoveryWithDefaultCache", output)
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
                    provider-object = ""Akka.Discovery.Dns.Internal.AsyncDnsProvider, Akka.Discovery.Dns""
                    nameservers = [ 
                        ""1dot1dot1dot1.cloudflare-dns.com"", 
                        ""1.1.1.1"" ]
                    positive-ttl = never
                    
                }
            "), "DnsServiceDiscoveryWithoutCache", output)
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
                    provider-object = ""Akka.Discovery.Dns.Internal.AsyncDnsProvider, Akka.Discovery.Dns""
                    nameservers = [ 
                        ""1dot1dot1dot1.cloudflare-dns.com"", 
                        ""1.1.1.1"" ]
                    positive-ttl = 10s
                    
                }
            "), "DnsServiceDiscoveryWithFixedCacheTime", output)
{
    
}


public class DnsServiceDiscoveryWithTcpFallback(ITestOutputHelper output) : DnsServiceDiscoveryBaseSpec(
    ConfigurationFactory.ParseString($$"""
                {
                     akka.loglevel = DEBUG
                     akka.discovery {
                         method = akka-dns
                         akka-dns {
                            class = "Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns"
                         }
                     }
                     akka.io.dns.resolver = async-dns
                     akka.io.dns.async-dns {
                         provider-object = "{{typeof(ForceTcpDnsProvider).FullName }}, {{typeof(ForceTcpDnsProvider).Assembly.GetName().Name}}"
                         nameservers = [ 
                             "1dot1dot1dot1.cloudflare-dns.com", 
                             "1.1.1.1" ]
                     }
                 }
     """), "DnsServiceDiscoveryWithTcpFallback", output)
{
     public class ForceTcpDnsProvider : AsyncDnsProvider
     {
         public override Type ActorClass { get; } = typeof(ForceTcpDnsClient);
     }
     internal class ForceTcpDnsClient(AsyncDnsCache cache, Configuration.Config config, EndPoint nameserver) : AsyncDnsClient(cache, config, nameserver)
    {
        // Override to force TCP mode by simulating truncation
        protected override void Ready(object message)
        {
            if (message is Udp.Received received)
            {
                // Get the data as a byte array
                var data = received.Data.ToArray();

                // DNS header: bytes 2-3 contain the flags field
                // TC flag is bit 9 (0-based, counting from the right) or 0x0200
                // We need to set this bit to indicate truncation
                if (data.Length >= 4) // Make sure we have at least the header
                {
                    // Set the TC bit in the flags field (network byte order)
                    data[2] |= 0x02; // Set bit 1 in byte 2 (the TC flag)

                    // Create a new received message with the modified data
                    var modifiedReceived = new Udp.Received(
                        ByteString.FromBytes(data),
                        received.Sender);

                    // Process with the modified data
                    base.Ready(modifiedReceived);
                    return;
                }
            }

            // For all other messages, use normal behavior
            base.Ready(message);

        }
    }
}

public abstract class DnsServiceDiscoveryBaseSpec(Configuration.Config config , string actorSystemName, ITestOutputHelper output) : TestKit.Xunit.TestKit(config, actorSystemName, output)
{
    internal virtual bool DoNotExpectAAAARecordsFromInetResolver { get; } = false;
    
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
        
        // skip this on windows for inet-resolver as it doesn't return AAAA 
        if (!DoNotExpectAAAARecordsFromInetResolver)
        {
            resolved.Addresses
                .Sum(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 1 : 0)
                .Should().BeGreaterThan(0, "At least one IPv6 record should be found");
        }

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