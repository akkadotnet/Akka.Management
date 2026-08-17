# Akka.Discovery.Redis

Redis-based service discovery for Akka.NET. Each node registers itself in Redis under a
time-to-live (TTL) key and refreshes it on a heartbeat; peers discover one another by scanning
those keys. It is a lightweight alternative to `Akka.Discovery.Azure` for cluster member discovery,
well suited to .NET Aspire local development and any environment that already runs Redis.

Dead nodes are removed automatically by Redis key expiry — there is no separate pruning process —
and stale (but not-yet-expired) nodes are filtered out of lookups via a configurable
`stale-ttl-threshold`.

## Installation

```
dotnet add package Akka.Discovery.Redis
```

## Usage with Akka.Hosting

```csharp
using Akka.Discovery.Redis;
using Akka.Hosting;

builder.Services.AddAkka("MySystem", akka =>
{
    akka
        .WithClustering()
        .WithClusterBootstrap(options =>
        {
            options.ContactPointDiscovery.ServiceName = "my-service";
            options.ContactPointDiscovery.RequiredContactPointsNr = 3;
        }, autoStart: true)
        .WithRedisDiscovery("localhost:6379");
});
```

`WithRedisDiscovery` only registers the discovery plugin; pair it with `WithClusterBootstrap` for
automated cluster formation.

### Options

```csharp
akka.WithRedisDiscovery(options =>
{
    options.ConnectionString = "localhost:6379";
    options.ServiceName = "my-service";
    options.StaleTtlThreshold = TimeSpan.FromSeconds(90);
    options.OperationTimeout = TimeSpan.FromSeconds(10);
});
```

## Configuration

The plugin reads from `akka.discovery.redis`. See `reference.conf` for the full set of keys and
their defaults, including `ttl`, `ttl-heartbeat-interval`, `stale-ttl-threshold`,
`operation-timeout`, `retry-backoff`, `max-retry-backoff`, and `read-only`.

## Resilience

- The Redis connection is established lazily inside the discovery guardian actor, so an unreachable
  Redis at startup never blocks or fails ActorSystem startup — the plugin retries with exponential
  backoff and recovers once Redis is reachable.
- Every Redis operation is bounded by `operation-timeout`.
- The heartbeat refreshes each node's TTL key; if a node stops heartbeating, Redis expires the key
  and the node drops out of discovery.

## License

Apache-2.0
