# Kubernetes Lease
This module is an implementation of an Akka Coordination Lease backed by a [Custom Resource Definition (CRD)](https://kubernetes.io/docs/concepts/extend-kubernetes/api-extension/custom-resources/) in Kubernetes. Resources in Kubernetes offer concurrency control and consistency that have been used to build a distributed lease/lock.

A lease can be used for:

* [Split Brain Resolver](https://getakka.net/articles/clustering/split-brain-resolver.html). An additional safety measure so that only one SBR instance can make the decision to remain up.
* [Cluster Singleton](https://getakka.net/articles/clustering/cluster-singleton.html). A singleton manager can be configured to acquire a lease before creating the singleton.
* [Cluster Sharding](https://getakka.net/articles/clustering/cluster-sharding.html). Each Shard can be configured to acquire a lease before creating entity actors.

In all cases the use of the lease increases the consistency of the feature. However, as the Kubernetes API server and its backing `etcd` cluster can also be subject to failure and network issues any use of this lease can reduce availability.

## Lease Instances

* With Split Brain Resolver there will be one lease per Akka Cluster
* With multiple Akka Clusters using SBRs in the same namespace, you must ensure different `ActorSystem` names because they all need a separate lease.
* With Cluster Sharding and Cluster Singleton there will be more leases
    * For Cluster Singleton there will be one per singleton.
    * For Cluster Sharding, there will be one per shard per type.

## Configuring

### Creating the Custom Resource Definition for the lease

This requires admin privileges to your Kubernetes / Open Shift cluster but only needs doing once.

Kubernetes:
```
kubectl apply -f lease.yml
```

Where lease.yml contains:
```yaml
apiVersion: apiextensions.k8s.io/v1
kind: CustomResourceDefinition
metadata:
  # name must match the spec fields below, and be in the form: <plural>.<group>
  name: leases.akka.io
spec:
  group: akka.io
  versions:
    - name: v1
      storage: true
      served: true
      schema:
        openAPIV3Schema:
          type: object
          properties:
            spec:
              type: object
              properties:
                owner:
                  type: string
                version:
                  type: string
                time:
                  type: integer
  scope: Namespaced
  names:
    # kind is normally the CamelCased singular type. Your resource manifests use this.
    kind: Lease
    listKind: LeaseList
    # singular name to be used as an alias on the CLI and for display
    singular: lease
    # plural name to be used in the URL: /apis/<group>/<version>/<plural>
    plural: leases
```

### Role based access control

Each pod needs permission to read/create and update lease resources. They only need access for the namespace they are in.

An example RBAC that can be used:

```yaml
kind: Role
apiVersion: rbac.authorization.k8s.io/v1
metadata:
  name: lease-access
rules:
  - apiGroups: ["akka.io"]
    resources: ["leases"]
    verbs: ["get", "create", "update", "list"]
---
kind: RoleBinding
apiVersion: rbac.authorization.k8s.io/v1
metadata:
  name: lease-access
subjects:
  - kind: User
    name: system:serviceaccount:<YOUR NAMSPACE>:default
roleRef:
  kind: Role
  name: lease-access
  apiGroup: rbac.authorization.k8s.io
```

This defines a `Role` that is allows to `get`, `create` and `update` lease objects and a `RoleBinding` that gives the default service user this role in `<YOUR NAMESPACE>`.

Future versions may also require delete access for cleaning up old resources. Current uses within Akka only create a single lease so cleanup is not an issue.

To avoid giving an application the access to create new leases an empty lease can be created in the same namespace as the application with:

```shell
kubelctl create -f sbr-lease.yml -n <YOUR_NAMESPACE>
```

Where sbr-lease.yml contains:

```yaml
apiVersion: "akka.io/v1"
kind: Lease
metadata:
  name: <YOUR_ACTORSYSTEM_NAME>-akka-sbr
spec:
  owner: ""
  time: 0
```

> __NOTE__
> 
> The lease gets created only during an actual Split Brain.

### Enable in SBR

To enable the lease for use within SBR:

```
akka.cluster {
    downing-provider-class = "Akka.Cluster.SBR.SplitBrainResolverProvider, Akka.Cluster"
    split-brain-resolver {
        active-strategy = lease-majority
        lease-majority {
            lease-implementation = "akka.coordination.lease.kubernetes"
        }
    }
}
```

## Full configuration options

```
akka.coordination.lease.kubernetes {

    lease-class = "Akka.Coordination.KubernetesApi.KubernetesLease, Akka.Coordination.KubernetesApi"

    api-ca-path = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt"
    api-token-path = "/var/run/secrets/kubernetes.io/serviceaccount/token"

    api-service-host-env-name = "KUBERNETES_SERVICE_HOST"
    api-service-port-env-name = "KUBERNETES_SERVICE_PORT"

    # Namespace file path. The namespace is to create the lock in. Can be overridden by "namespace"
    #
    # If this path doesn't exist, the namespace will default to "default".
    namespace-path = "/var/run/secrets/kubernetes.io/serviceaccount/namespace"

    # Namespace to create the lock in. If set to something other than "<namespace>" then overrides any value
    # in "namespace-path"
    namespace = "<namespace>"

    # How often to write time into CRD so that if the holder crashes
    # another node can take the lease after a given timeout. If left blank then the default is
    # max(5s, heartbeat-timeout / 10) which will be 12s with the default heartbeat-timeout
    heartbeat-interval = ""

    # How long a lease must not be updated before another node can assume
    # the holder has crashed.
    # If the lease holder hasn't crashed its next heart beat will fail due to the version
    # having been updated
    heartbeat-timeout = 120s

    # The individual timeout for each HTTP request. Defaults to 2/5 of the lease-operation-timeout
    # Can't be greater than then lease-operation-timeout
    api-service-request-timeout = ""

    # Use TLS & auth token for communication with the API server
    # set to false for plain text with no auth
    secure-api-server = true

    # The amount of time to wait for a lease to be aquired or released. This includes all requests to the API
    # server that are required. If this timeout is hit then the lease *may* be taken due to the response being lost
    # on the way back from the API server but will be reported as not taken and can be safely retried.
    lease-operation-timeout = 5s
}
```