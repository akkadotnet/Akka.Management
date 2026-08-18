# Akka.Aspire

.NET Aspire **service (client) integration** for Akka.NET clusters. Call `WithAspireClusterBootstrap`
in your service and it configures Akka.Remote, Akka.Cluster, Akka.Management, Cluster Bootstrap, and a
cluster-membership health check from the configuration that the
[`Akka.Aspire.Hosting`](https://www.nuget.org/packages/Akka.Aspire.Hosting) AppHost package injects —
no manual HOCON required.

## Installation

```
dotnet add package Akka.Aspire
```

## Usage (service)

```csharp
using Akka.Aspire;
using Akka.Discovery.Redis;
using Akka.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAkka("MySystem", (akkaBuilder, sp) =>
{
    akkaBuilder.WithAspireClusterBootstrap(sp,
        configureDiscovery: (b, config) =>
        {
            // The AppHost injects the discovery resource's name; fall back to a literal for clarity.
            var connectionStringName = config["Akka:Cluster:Clustering:ConnectionStringName"] ?? "akka-discovery";
            var redisConn = config.GetConnectionString(connectionStringName);
            if (!string.IsNullOrEmpty(redisConn))
                b.WithRedisDiscovery(redisConn, config["Akka:Cluster:ServiceName"]);
        },
        clusterConfigure: c => c.Roles = ["my-service"]);
});

builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/healthz");
app.MapGet("/", () => "Hello from Akka.NET Aspire!");
app.Run();
```

`WithAspireClusterBootstrap` reads the Aspire-injected environment variables and stands up the full
cluster stack. The `configureDiscovery` callback wires the discovery plugin using the same
`IConfiguration` that Aspire populates, and `clusterConfigure` sets roles and other cluster options.

## Swapping discovery providers

Only the AppHost resource and the `configureDiscovery` callback change between environments — the rest
of the service stays identical.

**Azure Table Storage** (local dev via Azurite, production via real Azure):
```csharp
akkaBuilder.WithAspireClusterBootstrap(sp,
    configureDiscovery: (b, config) =>
    {
        var name = config["Akka:Cluster:Clustering:ConnectionStringName"] ?? "akka-discovery";
        var conn = config.GetConnectionString(name);
        if (!string.IsNullOrEmpty(conn))
            b.WithAzureDiscovery(conn, config["Akka:Cluster:ServiceName"]);
    },
    clusterConfigure: c => c.Roles = ["my-service"]);
```

**Kubernetes** (no connection string needed):
```csharp
akkaBuilder.WithAspireClusterBootstrap(sp,
    configureDiscovery: (b, config) => b.WithKubernetesDiscovery(),
    clusterConfigure: c => c.Roles = ["my-service"]);
```

## Health checks

`WithAspireClusterBootstrap` registers a cluster-membership health check tagged `readiness`, so a
`/healthz/ready` probe reports Healthy only once the node has joined the cluster (`MemberStatus.Up`):

```csharp
app.MapHealthChecks("/healthz/ready",
    new HealthCheckOptions { Predicate = c => c.Tags.Contains("readiness") });
```

## Learn more

- [Akka.NET Clustering](https://getakka.net/articles/clustering/cluster-overview.html)
- [Akka.Management & Cluster Bootstrap](https://github.com/akkadotnet/Akka.Management)
- [Akka.Hosting](https://github.com/akkadotnet/Akka.Hosting)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire)

## License

Apache-2.0
