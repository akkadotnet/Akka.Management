# Akka.Aspire.Hosting

.NET Aspire **hosting integration** for Akka.NET clusters. Add it to your Aspire AppHost to declare a
cluster's topology — discovery backend, replica count, ports — and every service replica wired with
`WithReference` will automatically discover its peers, form a cluster, and report health.

Pair it with the service-side [`Akka.Aspire`](https://www.nuget.org/packages/Akka.Aspire) package,
which reads the configuration this package injects.

## Installation

```
dotnet add package Akka.Aspire.Hosting
```

## Usage (AppHost)

```csharp
using Akka.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// A discovery-backend Aspire resource (Redis here; Azure Table Storage is also supported)
var redis = builder.AddRedis("akka-discovery");

// Declare the Akka.NET cluster and bind its discovery backend
var akka = builder.AddAkka("my-cluster")
    .WithClustering(redis);

// Wire the cluster into a replicated service
builder.AddProject<Projects.MyService>("service")
    .WithHttpEndpoint(name: "http")
    .WithReplicas(3)
    .WithReference(akka);

builder.Build().Run();
```

`AddAkka` declares the cluster; `WithClustering(resource)` binds the discovery backend (the provider
type is auto-detected from the resource — e.g. `Redis`, `AzureTableStorage`); and `WithReference(akka)`
injects the resulting configuration into each service replica.

## What it injects

`WithReference(akka)` sets these environment variables on each replica, which the service-side
`Akka.Aspire` package reads via `IConfiguration`:

| Variable | Purpose |
|----------|---------|
| `Akka__Cluster__Enabled` | Enables clustering |
| `Akka__Cluster__RemotePort` / `Akka__Cluster__ManagementPort` | Unique ports per replica |
| `Akka__Cluster__PublicHostName` / `Akka__Cluster__ServiceName` | Discovery identity |
| `Akka__Cluster__RequiredContactPointsNr` | Derived from the replica count |
| `Akka__Cluster__Clustering__ProviderType` | Auto-detected from the resource type |
| `Akka__Cluster__Clustering__ConnectionStringName` | The Aspire resource name for the discovery backend |
| `ConnectionStrings__<name>` | The connection string for that backend |

## Supported discovery backends

- **Redis** — `builder.AddRedis(...)` + service-side [`Akka.Discovery.Redis`](https://www.nuget.org/packages/Akka.Discovery.Redis)
- **Azure Table Storage** — `builder.AddAzureStorage(...).AddTables(...)` + [`Akka.Discovery.Azure`](https://www.nuget.org/packages/Akka.Discovery.Azure)
- **Kubernetes** — service-side [`Akka.Discovery.KubernetesApi`](https://www.nuget.org/packages/Akka.Discovery.KubernetesApi) (no connection string needed)

Complete Redis and Azure Table Storage samples live under `src/aspire/examples/` in the
[Akka.Management](https://github.com/akkadotnet/Akka.Management) repository.

## Learn more

- [Akka.NET Clustering](https://getakka.net/articles/clustering/cluster-overview.html)
- [Akka.Management & Cluster Bootstrap](https://github.com/akkadotnet/Akka.Management)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire)

## License

Apache-2.0
