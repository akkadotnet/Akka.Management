# Azure Blob Storage Lease
This module is an implementation of an Akka Coordination Lease backed by [Azure Blob Storage](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blobs-introduction) in Azure. Resources in Azure can offer concurrency control and consistency that have been used to build a distributed lease/lock.

A lease can be used for:

* [Split Brain Resolver](https://getakka.net/articles/clustering/split-brain-resolver.html). An additional safety measure so that only one SBR instance can make the decision to remain up.
* [Cluster Singleton](https://getakka.net/articles/clustering/cluster-singleton.html). A singleton manager can be configured to acquire a lease before creating the singleton.
* [Cluster Sharding](https://getakka.net/articles/clustering/cluster-sharding.html). Each Shard can be configured to acquire a lease before creating entity actors.

In all cases the use of the lease increases the consistency of the feature. However, as the Azure Blob Storage server can also be subject to failure and network issues any use of this lease can reduce availability.

## Lease Instances

* With Split Brain Resolver there will be one lease per Akka Cluster
* With multiple Akka Clusters using SBRs in the same namespace, you must ensure different `ActorSystem` names because they all need a separate lease.
* With Cluster Sharding and Cluster Singleton there will be more leases
    * For Cluster Singleton there will be one per singleton.
    * For Cluster Sharding, there will be one per shard per type.

## Configuring

### Enable In SBR Using Akka.Cluster.Hosting

To enable Azure lease inside SBR, you need to pass a `LeaseMajorityOption` instance into the second parameter of the `WithClustering()` extension method and specify that you're using the Azure lease implementation.

```csharp
using var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddAkka("azureLeaseDemo", (builder, provider) =>
        {
            builder
                .WithRemoting("<akka-node-host-name-or-ip>", 4053)
                .WithClustering(sbrOption: new LeaseMajorityOption
                {
                    LeaseImplementation = "akka.coordination.lease.azure",
                    LeaseName = "myActorSystem-akka-sbr"
                })
                .WithAzureLease("<your-Azure-Blob-Storage-connection-string>");
        });
    }).Build();

await host.RunAsync();
```

### Enable In SBR Using HOCON Configuration

To enable the lease for use within SBR:

```
akka.cluster {
    downing-provider-class = "Akka.Cluster.SBR.SplitBrainResolverProvider, Akka.Cluster"
    split-brain-resolver {
        active-strategy = lease-majority
        lease-majority {
            lease-implementation = "akka.coordination.lease.azure"
        }
    }
}
```

## Full configuration options

akka.coordination.lease.azure {

    lease-class = "Akka.Coordination.Azure.AzureLease, Akka.Coordination.Azure"

    connection-string = ""
    
    # Container to create the lock in.
    container-name = "akka-coordination-lease"
    
    # How often to write time into CRD so that if the holder crashes
    # another node can take the lease after a given timeout. If left blank then the default is
    # max(5s, heartbeat-timeout / 10) which will be 12s with the default heartbeat-timeout
    heartbeat-interval = ""

    # How long a lease must not be updated before another node can assume the holder has crashed.
    # If the lease holder hasn't crashed its next heart beat will fail due to the version
    # having been updated
    heartbeat-timeout = 120s

    # The individual timeout for each HTTP request. Defaults to 2/5 of the lease-operation-timeout
    # Can't be greater than then lease-operation-timeout
    api-service-request-timeout = ""
}
