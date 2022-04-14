# Akka.Coordination.KubernetesApi Stress Test
This project is meant to serve as a demonstration of how Akka.Management Kubernetes discovery bootstraps a 10 node cluster automatically and demonstrate two cases of node failure and how the cluster will recover using the kubernetes lease based split brain resolver.

## Running the Test

First, build all of the benchmark images by running the following command at the root of the repository directory:

```
PS> ./build.cmd docker
```

Start the `deploy.cmd` batch script inside `k8s` folder. This will start a 10 StatefulSet kubernetes node.

```
PS> ./deploy.cmd
```

Since Cluster.Bootstrap is set using the `LowestAddressJoinDecider`, pod 0 of the kubernetes cluster will always be the leader of the cluster. 

Lets peek at what is happening inside pod 0, open a new console window and stream its console log:

```
PS> kubectl logs stress-test-0 -n stress-test -f
```

You will see pod 0 log streamed into the console window, this will show how Cluster.Bootstrap and Akka.Discovery.KubernetesApi work. Once all 10 pods are up and stable, we'll try to kill pod 2:

```
kubectl exec stress-test-2 -n stress-test -it -- pbm test crash
```

Inside pod 0, you will see the SBR acquiring a lease by these logs:

```
[DEBUG][04/14/2022 14:46:42][Thread 0025][akka.tcp://ClusterSys@stress-test-0.stress-test:4053/system/cluster/core/daemon/downingProvider] SBR trying to acquire lease
[DEBUG][04/14/2022 14:46:42][Thread 0025][KubernetesLease (akka://ClusterSys)] Acquiring lease
[DEBUG][04/14/2022 14:46:42][Thread 0036][akka.tcp://ClusterSys@stress-test-0.stress-test:4053/user/KubernetesLease1] [Idle] Received Acquire, ReadRequired.
[DEBUG][04/14/2022 14:46:42][Thread 0036][KubernetesApiImpl (akka://ClusterSys)] Resource does not exist: clustersys-akka-sbr
[INFO][04/14/2022 14:46:42][Thread 0036][KubernetesApiImpl (akka://ClusterSys)] lease clustersys-akka-sbr does not exist, creating
[DEBUG][04/14/2022 14:46:42][Thread 0034][KubernetesApiImpl (akka://ClusterSys)] Lease resource created
[DEBUG][04/14/2022 14:46:42][Thread 0034][KubernetesApiImpl (akka://ClusterSys)] Converting Akka.Coordination.KubernetesApi.Models.LeaseCustomResource
[DEBUG][04/14/2022 14:46:42][Thread 0025][akka.tcp://ClusterSys@stress-test-0.stress-test:4053/user/KubernetesLease1] [PendingRead] Lease has not been taken, trying to get lease.
[DEBUG][04/14/2022 14:46:42][Thread 0025][KubernetesApiImpl (akka://ClusterSys)] Updating clustersys-akka-sbr to Akka.Coordination.KubernetesApi.Models.LeaseCustomResource
[DEBUG][04/14/2022 14:46:42][Thread 0035][KubernetesApiImpl (akka://ClusterSys)] Lease after update: {"apiVersion":"akka.io/v1","kind":"Lease","metadata":{"creationTimestamp":"2022-04-14T14:46:42Z","generation":2,"managedFields":[{"apiVersion":"akka.io/v1","fieldsType":"FieldsV1","fieldsV1":{"f:spec":{".":{},"f:owner":{},"f:time":{}}},"manager":"unknown","operation":"Update","time":"2022-04-14T14:46:42Z"}],"name":"clustersys-akka-sbr","namespace":"stress-test","resourceVersion":"332045","uid":"103bff95-6b73-43ce-a532-9b6b5173692f"},"spec":{"owner":"ClusterSys@stress-test-0.stress-test:4053","time":53202751}}
[DEBUG][04/14/2022 14:46:42][Thread 0035][KubernetesApiImpl (akka://ClusterSys)] Converting Akka.Coordination.KubernetesApi.Models.LeaseCustomResource
[DEBUG][04/14/2022 14:46:42][Thread 0035][ActorSystem(ClusterSys)] Start timer [heartbeat] with generation [1]
[INFO][04/14/2022 14:46:42][Thread 0034][akka.tcp://ClusterSys@stress-test-0.stress-test:4053/system/cluster/core/daemon/downingProvider] SBR acquired lease for decision [Akka.Cluster.SBR.AcquireLeaseAndDownUnreachable]
```

A few moment later, pod 0 will shut down, this is intentional to show what happens if a leader crashed when it is holding a lease. The whole cluster will be downed and restarted and should reform afterward.