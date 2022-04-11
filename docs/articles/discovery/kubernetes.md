---
uid: discovery-k8s
title: Akka Discovery for Kubernetes
---
# Akka Discovery for Kubernetes

The Kubernetes API can be used to discover peers and form an Akka Cluster. The `KubernetesApi` mechanism queries the Kubernetes API server to find pods with a given label. A Kubernetes service isn’t required for the cluster bootstrap but may be used for external access to the application.

To find other pods, this discovery method needs to know how they are labeled, what the name of the target port is, and what namespace they reside in. Below, you’ll find the default configuration. It can be customized by changing these values in your HOCON configuration.

```
akka.discovery {
  # Set the following in your application.conf if you want to use this discovery mechanism:
  method = kubernetes-api
  kubernetes-api {
    class = "Akka.Discovery.KubernetesApi.KubernetesApiServiceDiscovery, Akka.Discovery.KubernetesApi"

    # API server, cert and token information. Currently these are present on K8s versions: 1.6, 1.7, 1.8, and perhaps more
    api-ca-path = "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt"
    api-token-path = "/var/run/secrets/kubernetes.io/serviceaccount/token"
    api-service-host-env-name = "KUBERNETES_SERVICE_HOST"
    api-service-port-env-name = "KUBERNETES_SERVICE_PORT"

    # Namespace discovery path
    #
    # If this path doesn't exist, the namespace will default to "default".
    pod-namespace-path = "/var/run/secrets/kubernetes.io/serviceaccount/namespace"

    # Namespace to query for pods.
    #
    # Set this value to a specific string to override discovering the namespace using pod-namespace-path.
    pod-namespace = "<pod-namespace>"

    # Domain of the k8s cluster
    pod-domain = "cluster.local"

    # Selector value to query pod API with.
    # `{0}` will be replaced with the configured effective name, which defaults to the actor system name
    pod-label-selector = "app={0}"

    # Enables the usage of the raw IP instead of the composed value for the resolved target host
    use-raw-ip = true

    # When set, validate the container is not in 'waiting' state
    container-name = ""
  }
}
```

## Using Discovery Together with Akka.Management and Cluster.Bootstrap
All discovery plugins are designed to work with Cluster.Bootstrap to provide an automated way to form a cluster that is not based on hard wired seeds configuration. Some HOCON configuration is needed to make discovery work with Cluster.Bootstrap:

```
akka.discovery.method = kubernetes-api
akka.management.http.routes = {
    cluster-bootstrap = "Akka.Management.Cluster.Bootstrap.ClusterBootstrapProvider, Akka.Management.Cluster.Bootstrap"
}
```

You then start the cluster bootstrapping process by calling:
```C#
await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();
```

A more complete example:
```C#
var config = ConfigurationFactory
    .ParseString(File.ReadAllText("application.conf"))
    .WithFallback(ClusterBootstrap.DefaultConfiguration())
    .WithFallback(AkkaManagementProvider.DefaultConfiguration())
    .WithFallback(KubernetesDiscovery.DefaultConfiguration())
    .WithFallback(DiscoveryProvider.DefaultConfiguration());

var system = ActorSystem.Create("my-system", config);
var log = Logging.GetLogger(system, this);

await AkkaManagement.Get(system).Start();
await ClusterBootstrap.Get(system).Start();

var cluster = Cluster.Get(system);
cluster.RegisterOnMemberUp(() => {
  var upMembers = cluster.State.Members
      .Where(m => m.Status == MemberStatus.Up)
      .Select(m => m.Address.ToString());

  log.Info($"Current up members: [{string.Join(", ", upMembers)}]")
});
```

## Configuration
### Kubernetes YAML Configuration
Below is an example of a YAML example taken from our [integration sample](https://github.com/akkadotnet/akka.net-integration-tests/tree/master/src/ClusterBootstrap).
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: clusterbootstrap
---
apiVersion: v1
kind: Service
metadata:
  name: clusterbootstrap
  namespace: clusterbootstrap
  labels:
    app: clusterbootstrap
spec:
  clusterIP: None
  ports:
  - port: 4053
    name: akka-remote
  - port: 8558 
    name: management
  selector:
    app: clusterbootstrap
---
apiVersion: apps/v1
kind: StatefulSet
metadata:
  namespace: clusterbootstrap
  name: clusterbootstrap
  labels:
    app: clusterbootstrap
spec:
  serviceName: clusterbootstrap
  replicas: 10
  selector:
    matchLabels:
      app: clusterbootstrap
  template:
    metadata:
      labels:
        app: clusterbootstrap
    spec:
      terminationGracePeriodSeconds: 35
      dnsConfig:
        options:
        - name: use-vc
      containers:
      - name: clusterbootstrap
        image: akka.cluster.bootstrap:0.1.0
        env:
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: CLUSTER_IP
          value: "$(POD_NAME).clusterbootstrap"
        livenessProbe:
          tcpSocket:
            port: akka-remote
        ports:
        - containerPort: 8558
          protocol: TCP
          name: management
        - containerPort: 4053
          protocol: TCP
          name: akka-remote
```

