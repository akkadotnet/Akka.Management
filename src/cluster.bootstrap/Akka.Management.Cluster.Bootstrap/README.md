# Akka.Management.Cluster.Bootstrap
Akka Cluster Bootstrap helps forming (or joining to) a cluster by using Akka Discovery to discover peer nodes. 
It is an alternative to configuring static seed-nodes in dynamic deployment environments such as on Kubernetes or AWS.

It builds on the flexibility of Akka Discovery, leveraging a range of discovery mechanisms depending on the environment you 
want to run your cluster in.

Cluster bootstrap depends on:
- Akka.Discovery to discover other members of the cluster
- Akka.Management to host HTTP endpoints used during the bootstrap process

## Usage
Akka management must be started as well as the bootstrap process, this can either be done through config or programmatically.

### Setting Up Cluster.Bootstrap from HOCON Configuration

Listing the ClusterBootstrap extension among the autoloaded akka.extensions in your configuration will cause it to autostart:

```
# trigger autostart by loading the extension through config
akka.extensions = ["Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap"]
```

If management or bootstrap configuration is incorrect, the autostart will log an error and terminate the actor system.

### Setting Up Cluster.Bootstrap Programmatically
```
// Akka Management hosts the HTTP routes used by bootstrap
await AkkaManagement.Get(system).Start();

// Starting the bootstrap process needs to be done explicitly
await ClusterBootstrap.Get(system).Start();
```

Ensure that `seed-nodes` is not present in configuration and that either autoloading through config or start() is called on every node.

The following configuration is required, more details for each and additional configuration can be found in the reference configuration:

- `akka.management.cluster.bootstrap.contact-point-discovery.service-name`: a unique name in the deployment environment for this 
  cluster instance which is used to lookup peers in service discovery. If unset, it will be derived from the ActorSystem name.
- `akka.management.cluster.bootstrap.contact-point-discovery.discovery-method`: the intended service discovery mechanism 
  (from what choices [Akka Discovery](https://getakka.net/articles/discovery/index.html) provides). If unset, falls back to the system-wide default from akka.discovery.method.

## How It Works

- Each node exposes an HTTP endpoint `/bootstrap/seed-nodes`. This is provided by Akka.Management.Cluster.Bootstrap and 
  exposed automatically by starting Akka.Management.
- `/bootstrap/seed-nodes` will query its internal cluster state and returns a list of members that are either in the up, weakly up, 
    or joining state
- During bootstrap each node queries service discovery repeatedly to get the initial contact points until at least the number 
  of contact points (and recommended exactly equal) as defined in contact-point-discovery.required-contact-point-nr has been found.
- Each node then probes these found contact points `/bootstrap/seed-nodes` endpoint to see if a cluster has already been formed
  - If there is an existing cluster, it joins the cluster and bootstrapping is finished.
  - If no cluster exists and every node returns an empty list of seed-nodes, The node with the lowest address from the set of 
    contact points forms a new cluster.
  - Other nodes will start to see the `/bootstrap/seed-nodes` of the node that self-joined and will join its cluster.

Please see the [complete bootstrap process documentation](./docs/BOOTSTRAP_PROCESS.md) for more information.

## Joining Mechanism Precedence
As Akka Cluster allows nodes to join to a cluster using multiple different methods, the precedence of each method is strictly defined and is as follows:


- If akka.cluster.seed-nodes (in your HOCON configuration) are non-empty, those nodes will be joined, 
  and bootstrap will NOT execute even if start() is called or autostart through configuration is enabled, 
  however a warning will be logged.
- If an explicit Cluster.Join or Cluster.JoinSeedNodes is invoked before the bootstrap completes, 
  that joining would take precedence over the bootstrap (but it’s not recommended to do so, see below).
- The Cluster Bootstrap mechanism takes some time to complete, but eventually issues a joinSeednodes.

> [!WARNING]
> It is __NOT__ recommended to mix various joining mechanisms. Pick one mechanism and stick to it in order to avoid any surprises 
> during cluster formation. E.g. do __NOT__ set akka.cluster.seed-nodes and do __NOT__ call Cluster.Join if you are going to be 
> using the Bootstrap mechanism.

## Deployment Considerations
### Initial deployment
Cluster Bootstrap will always attempt to join an existing cluster if possible. However if no other contact point advertises 
any `seed-nodes`, a new cluster will be formed by the node decided by the `JoinDecider` where the default sorts the addresses 
then picks the lowest.

The HOCON setting `akka.management.cluster.bootstrap.new-cluster-enabled` can be used to disable new cluster formation and 
to only allow the node to join existing clusters. 


- On initial deployment use the default akka.management.cluster.bootstrap.new-cluster-enabled=on
- Following the initial deployment it is recommended to set `akka.management.cluster.bootstrap.new-cluster-enabled=off` with an 
  immediate re-deployment once the initial cluster has formed

This can be used to provide additional safety during restarts and redeploys while there is a network partition present. 
Without new cluster formation disabled, an isolated set of nodes could form a new cluster, creating a split-brain.

### Recommended Configuration
When using the bootstrap module, there are some underlying Akka Cluster settings that should be specified to ensure that 
your deployment is robust.

Since the target environments for this module are dynamic, that is, instances can come and go, failure needs to be considered. 
The following configuration will result in your application being shut down after 30 seconds if it is unable to join the 
discovered seed nodes. In this case, the orchestrator (i.e. Kubernetes or Marathon) will restart your node and the operation will 
(presumably) eventually succeed. You will want to specify the following in your HOCON configuration:

```
akka.cluster.shutdown-after-unsuccessful-join-seed-nodes = 30s
```

### Rolling Updates
#### Graceful Shutdown
Akka Cluster can handle hard failures using a downing provider such as the split brain resolver discussed below. 
However this should not be relied upon for regular rolling redeploys. Features such as ClusterSingletons and ClusterSharding 
can safely restart actors on new nodes far quicker when it is certain that a node has shutdown rather than crashed.

Graceful leaving will happen with the default settings as it is part of Coordinated Shutdown. 
Just ensure that a node is sent a SIGTERM and not a SIGKILL. Environments such as Kubernetes will do this, 
it is important to ensure that if the CLR is wrapped with a script that it forwards the signal.

Upon receiving a SIGTERM Coordinated Shutdown will:

- Perform a Cluster.Get(system).Leave() on itself
- The status of the member will be changed to Exiting while allowing any shards to be shutdown gracefully 
  and ClusterSingletons to be migrated if this was the oldest node. Finally the node is removed from the Akka Cluster membership.

#### Number of Nodes to Redeploy At Once
Akka bootstrap requires a `stable-period` where service discovery returns a stable set of contact points. 
When doing rolling updates, it is best to wait for a node (or group of nodes) to finish joining the cluster before adding
and removing other nodes.

#### Cluster Singleton
`ClusterSingleton`s run on the oldest node in the cluster. To avoid singletons moving during every node deployment, 
it is advised to start a rolling redeploy starting at the newest node. Then ClusterSingletons only move once. 
This is the default behaviour for Kubernetes deployments. Cluster Sharding uses a singleton internally so this is important, 
even if not using singletons directly.

### Split Brains and Ungraceful Shutdown
Nodes can crash causing cluster members to become unreachable. This is a tricky problem as it is not possible to distinguish 
between a network partition and a node failure. To rectify this in an automated manner, make sure you enable the 
Split Brain Resolver. This module has a number of strategies that can ensure that the cluster continues to function 
during network partitions and node failures.

## Reference Configuration
```
######################################################
# Akka Cluster Bootstrap Config                      #
######################################################

akka.management {

  # registers bootstrap routes to be included in akka-management's http endpoint
  http.routes {
    cluster-bootstrap = "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap"
  }

  cluster.bootstrap {

    # Cluster Bootstrap will always attempt to join an existing cluster if possible. However
    # if no contact point advertises any seed-nodes a new cluster will be formed by the
    # node with the lowest address as decided by [[LowestAddressJoinDecider]].
    # Setting `new-cluster-enabled=off` after an initial cluster has formed is recommended to prevent new clusters
    # forming during a network partition when nodes are redeployed or restarted.
    new-cluster-enabled = on

    # Configuration for the first phase of bootstrapping, during which contact points are discovered
    # using the configured service discovery mechanism (e.g. DNS records).
    contact-point-discovery {

      # Define this name to be looked up in service discovery for "neighboring" nodes
      # If undefined, the name will be taken from the AKKA__CLUSTER__BOOTSTRAP__SERVICE_NAME
      # environment variable or extracted from the ActorSystem name
      service-name = "<service-name>"

      # The portName passed to discovery. This should be set to the name of the port for Akka Management
      # If set to "", `null` is passed to the discovery mechanism.
      port-name = ""

      # The protocol passed to discovery.
      # If set to "" None is passed.
      protocol = "tcp"

      # Added as suffix to the service-name to build the effective-service name used in the contact-point service lookups
      # If undefined, nothing will be appended to the service-name.
      #
      # Examples, set this to:
      # "default.svc.cluster.local" or "my-namespace.svc.cluster.local" for kubernetes clusters.
      service-namespace = "<service-namespace>"

      # The effective service name is the exact string that will be used to perform service discovery.
      #
      # Set this value to a specific string to override the default behaviour of building the effective name by
      # concatenating the `service-name` with the optional `service-namespace` (e.g. "name.default").
      effective-name = "<effective-name>"

      # Config path of discovery method to be used to locate the initial contact points.
      # It must be a fully qualified config path to the discovery's config section.
      #
      # By setting this to `akka.discovery` we ride on the configuration mechanisms that akka-discovery has,
      # and reuse what is configured for it. You can set it explicitly to something else here, if you want to
      # use a different discovery mechanism for the bootstrap than for the rest of the application.
      discovery-method = akka.discovery

      # Amount of time for which a discovery observation must remain "stable"
      # (i.e. discovered contact-points list did not change) before a join decision can be made.
      # This is done to decrease the likelyhood of performing decisions on fluctuating observations.
      #
      # This timeout represents a tradeoff between safety and quickness of forming a new cluster.
      stable-margin = 5s

      # Interval at which service discovery will be polled in search for new contact-points
      #
      # Note that actual timing of lookups will be the following:
      # - perform initial lookup; interval is this base interval
      # - await response within resolve-timeout
      #   (this can be larger than interval, which means interval effectively is resolveTimeout + interval,
      #    this has been specifically made so, to not hit discovery services with requests while the lookup is being serviced)
      #   - if failure happens apply backoff to interval (the backoff growth is exponential)
      # - if no failure happened, and we receive a resolved list of services, schedule another lookup in interval time
      #   - if previously failures happened during discovery, a successful lookup resets the interval to `interval` again
      # - repeat until stable-margin is reached
      interval = 1s

      # Adds "noise" to vary the intervals between retries slightly (0.2 means 20% of base value).
      # This is important in order to avoid the various nodes performing lookups in the same interval,
      # potentially causing a thundering herd effect. Usually there is no need to tweak this parameter.
      exponential-backoff-random-factor = 0.2

      # Maximum interval to which the exponential backoff is allowed to grow
      exponential-backoff-max = 15s

      # The smallest number of contact points that need to be discovered before the bootstrap process can start.
      # For optimal safety during cluster formation, you may want to set these value to the number of initial
      # nodes that you know will participate in the cluster (e.g. the value of `spec.replicas` as set in your kubernetes config.
      required-contact-point-nr = 2

      # Timeout for getting a reply from the service-discovery subsystem
      resolve-timeout = 3s

      # Does a succcessful response have to be received by all contact points.
      # Used by the LowestAddressJoinDecider
      # Can be set to false in environments where old contact points may still be in service discovery
      # or when using local discovery and cluster formation is desired without starting all the nodes
      # Required-contact-point-nr still needs to be met
      contact-with-all-contact-points = true
    }

    # Configured how we communicate with the contact point once it is discovered
    contact-point {

      # If no port is discovered along with the host/ip of a contact point this port will be used as fallback
      # Also, when no port-name is used and multiple results are returned for a given service with at least one
      # port defined, this port is used to disambiguate. 
      fallback-port = "<fallback-port>"

      # by default when no port-name is set only the contact points that contain the fallback-port
      # are used for probing. This makes the scenario where each akka node has multiple ports
      # returned from service discovery (e.g. management, remoting, front-end HTTP) work without
      # having to configure a port-name. If instead service discovery will return only akka management
      # ports without specifying a port-name, e.g. management has dynamic ports and its own service
      # name, then set this to false to stop the results being filtered
      filter-on-fallback-port = true

      # If some discovered seed node will keep failing to connect for specified period of time,
      # it will initiate rediscovery again instead of keep trying.
      probing-failure-timeout = 3s

      # Interval at which contact points should be polled
      # the effective interval used is this value plus the same value multiplied by the jitter value
      probe-interval = 1s

      # Max amount of jitter to be added on retries
      probe-interval-jitter = 0.2
    }

    join-decider {
      # Implementation of JoinDecider.
      # It must extend akka.management.cluster.bootstrap.JoinDecider and
      # have public constructor with ActorSystem and ClusterBootstrapSettings
      # parameters.
      class = "Akka.Management.Cluster.Bootstrap.LowestAddressJoinDecider, Akka.Management.Cluster.Bootstrap"
    }
  }
}
```
