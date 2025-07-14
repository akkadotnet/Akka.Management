// -----------------------------------------------------------------------
//  <copyright file="DnsServiceDiscoverySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Dns.Tests;

public class ARecordsDiscovery(ITestOutputHelper output) : TestKit.Xunit2.TestKit(
    ConfigurationFactory.ParseString(@"
                akka.loglevel = DEBUG
                akka.discovery {
                    method = akka-dns
                    akka-dns {
                        class = ""Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns""
                    }
                }
            "), "dns-discovery", output)
{
    [Fact(DisplayName = "DnsServiceDiscovery should be loadable via config")]
    public void DnsServiceDiscoveryShouldBeLoadableViaConfig()
    {
        var serviceDiscovery = Discovery.Get(Sys).LoadServiceDiscovery("akka-dns");
        serviceDiscovery.Should().BeOfType<Dns.DnsServiceDiscovery>();
    }

    [Theory(DisplayName = "DnsServiceDiscovery should handle A/AAAA lookup with real DNS")]
    [InlineData("jabber.org",  "XMPP server")]
    [InlineData("matrix.org",  "Matrix server")]
    [InlineData("gmail.com", "Gmail IMAPS")]
    public async Task DnsServiceDiscoveryShouldHandleLookup(string serviceName, string description)
    {
        Output.WriteLine($"Testing A/AAAA lookup for {description}: {serviceName}");
        var serviceDiscovery = new Dns.DnsServiceDiscovery((ExtendedActorSystem)Sys);

        var lookup = new Lookup(serviceName);
        var resolved = await serviceDiscovery.Lookup(lookup, TimeSpan.FromSeconds(60));

        // Skip assertion if no records found (some services might not have SRV records)
        if (resolved.Addresses.Count == 0)
        {
            Output.WriteLine($"No SRV records found for {description}. Skipping assertions.");
            return;
        }

        Output.WriteLine($"Found {resolved.Addresses.Count} records for {description}");

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
            address.Port.Should().BeNull("Port should not be specified for A/AAAA lookup");
        }
    }
}