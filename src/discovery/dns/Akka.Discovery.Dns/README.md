# Discovery via DNS

This module provides a service discovery mechanism that uses DNS to locate services. It supports both A/AAAA record resolution and SRV record resolution for service discovery.

DNS discovery can resolve services using:
- **A/AAAA records**: Standard DNS address records that return IP addresses
- **SRV records**: Service records that provide both IP addresses and port information

## Enabling DNS Discovery Using Akka.Hosting

To enable DNS discovery using Akka.Hosting, you can use the `WithDnsDiscovery` extension method:

```csharp
builder.WithDnsDiscovery();
```

For SRV record resolution, you also need to configure the async DNS resolver with custom nameservers:

```csharp
builder.WithAsyncDnsResolver(opt => 
{
    opt.Nameservers = [  // at least one nameserver is required
        "127.0.0.1:1053", // IPv4 with port
        "[fd::aa:aa:aa:aa]", // IPv6 without port (default is 53)
        "1dot1dot1dot1.cloudflare-dns.com" // Hostname
    ];
});
```

Note: The default port for DNS is 53, so if you don't specify a port, it will use 53. DNS hostname would be resolved using the default System.Net.Dns.GetHostAddresses method.

## Enabling DNS Discovery Using HOCON Configuration

To enable DNS discovery via HOCON, you will need to modify your HOCON configuration:

```text
akka.discovery.method = dns
```

Below, you'll find the default configuration. It can be customized by changing these values in your HOCON configuration:

```
akka.discovery.dns {
    class = "Akka.Discovery.Dns.DnsServiceDiscovery, Akka.Discovery.Dns"
}
```

To enable async-dns resolver, you need to configure it in your HOCON configuration:

```text
akka.io.dns.resolver = async-dns
akka.io.dns.async-dns {
    provider-object = "Akka.Discovery.Dns.Internal.AsyncDnsProvider, Akka.Discovery.Dns"
    nameservers = [ "127.0.0.1:53"; "my-dns.example.com"; "[::1]:53" ]
    cache-cleanup-interval = 120s
    positive-ttl = forever
  }
```

__NOTES__

- `async-dns` resolver doesn't use System.Net.Dns.GetHostAddresses method to resolve DNS hostname, therefore it has no way to determine DNS nameserver from the runtime environment. You need to configure it explicitly.
- `async-dns` resolver is required to resolve SRV records.
