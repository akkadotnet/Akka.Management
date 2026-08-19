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

## Corresponding AppHost (`Akka.Aspire.Hosting`)

The service above consumes the configuration that the
[`Akka.Aspire.Hosting`](https://www.nuget.org/packages/Akka.Aspire.Hosting) AppHost injects. The
matching AppHost declares the discovery backend and the cluster, then wires this service to it with
`WithReference`:

```csharp
using Akka.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// The discovery backend the service's `WithRedisDiscovery` connects to. Its resource name
// ("akka-discovery") surfaces on the service side as `Akka:Cluster:Clustering:ConnectionStringName`,
// so the two halves stay in sync automatically.
var redis = builder.AddRedis("akka-discovery");

// Declare the cluster and bind its discovery backend (the provider type is auto-detected).
var akka = builder.AddAkka("my-cluster")
    .WithClustering(redis);

// Every replica wired with WithReference discovers its peers and forms the cluster.
builder.AddProject<Projects.MyService>("service")
    .WithHttpEndpoint(name: "http")
    .WithReplicas(3)
    .WithReference(akka);

builder.Build().Run();
```

See the [`Akka.Aspire.Hosting`](https://github.com/akkadotnet/Akka.Management/blob/dev/src/aspire/Akka.Aspire.Hosting/README.md)
README for the full set of injected settings and supported discovery backends, and
`src/aspire/examples/` for complete runnable Redis and Azure Table Storage samples.

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
