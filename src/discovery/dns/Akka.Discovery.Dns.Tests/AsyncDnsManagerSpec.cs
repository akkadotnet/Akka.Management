using System;
using System.Net;
using Akka.Discovery.Dns.Internal;
using FluentAssertions;
using Xunit;

namespace Akka.Discovery.Dns.Tests;

public class AsyncDnsManagerSpec {

    [Fact]
    public void ParseEndPoint_ShouldParseIpv4WithPort()
    {
        var result = AsyncDnsManager.ParseEndPoint("192.168.1.1:53");
        var ipEndPoint = result as IPEndPoint;
        ipEndPoint.Should().NotBeNull();
        ipEndPoint!.Address.ToString().Should().Be("192.168.1.1");
        ipEndPoint.Port.Should().Be(53);
    }

    [Fact]
    public void ParseEndPoint_ShouldParseHostnameWithPort()
    {
        var result = AsyncDnsManager.ParseEndPoint("1dot1dot1dot1.cloudflare-dns.com:53");
        var ipEndPoint = result as IPEndPoint;
        ipEndPoint.Should().NotBeNull();
        //can be 1.0.0.1 or 1.1.1.1
        ipEndPoint!.Address.ToString().Should().StartWith("1.");
        ipEndPoint!.Address.ToString().Should().EndWith(".1");
        ipEndPoint.Port.Should().Be(53);
    }

    [Fact]
    public void ParseEndPoint_ShouldParseHostnameWithoutPort()
    {
        var result = AsyncDnsManager.ParseEndPoint("1dot1dot1dot1.cloudflare-dns.com");
        var ipEndPoint = result as IPEndPoint;
        ipEndPoint.Should().NotBeNull();
        //can be 1.0.0.1 or 1.1.1.1
        ipEndPoint!.Address.ToString().Should().StartWith("1.");
        ipEndPoint!.Address.ToString().Should().EndWith(".1");
        ipEndPoint.Port.Should().Be(53); // Default port is now 53
    }
    
    [Fact]
    public void ParseEndPoint_ShouldParseIpv6WithBracketsAndPort()
    {
        var result = AsyncDnsManager.ParseEndPoint("[2001:db8:1::2]:53");
        var ipEndPoint = result as IPEndPoint;
        ipEndPoint.Should().NotBeNull();
        ipEndPoint!.Address.ToString().Should().Be("2001:db8:1::2");
        ipEndPoint.Port.Should().Be(53);
    }

    [Fact]
    public void ParseEndPoint_ShouldHandleEmptyOrNullInput()
    {
        // Should throw ArgumentException for null or empty input
        Assert.Throws<ArgumentException>(() => AsyncDnsManager.ParseEndPoint(null!));
        Assert.Throws<ArgumentException>(() => AsyncDnsManager.ParseEndPoint(""));
        Assert.Throws<ArgumentException>(() => AsyncDnsManager.ParseEndPoint("  "));
    }
    
    [Fact]
    public void ParseEndPoint_ShouldHandleWrongFormatInput()
    {
        // Should throw ArgumentException for null or empty input
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("[11111:11111"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("[2001:db8:1::2]:whatever"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("[2001:db8:1::2]:"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("1.0.0.1:whatever"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("whatever:1"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("whatever:whatever"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("whatever:"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("whatever"));
        Assert.Throws<FormatException>(() => AsyncDnsManager.ParseEndPoint("whatever.local"));
    }

    [Fact]
    public void ParseEndPoint_ShouldParseIpv6WithoutPort()
    {
        var result = AsyncDnsManager.ParseEndPoint("2001:db8:1::2");
        var ipEndPoint = result as IPEndPoint;
        ipEndPoint.Should().NotBeNull();
        ipEndPoint!.Address.ToString().Should().Be("2001:db8:1::2");
        ipEndPoint.Port.Should().Be(53); // Default port is now 53
    }

    [Fact]
    public void ParseEndPoint_ShouldHandleIpWithoutPort()
    {
        var result = AsyncDnsManager.ParseEndPoint("192.168.1.1");
        var ipEndPoint = result as IPEndPoint;
        ipEndPoint.Should().NotBeNull();
        ipEndPoint.Address.ToString().Should().Be("192.168.1.1");
        ipEndPoint.Port.Should().Be(53);
    }

}