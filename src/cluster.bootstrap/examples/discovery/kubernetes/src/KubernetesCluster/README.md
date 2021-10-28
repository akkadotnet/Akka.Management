# Akka.ClusterBootstrap
This project is meant to serve as a demonstration of how Akka.Management.Cluster.Bootstrap bootstraps a 3 node cluster from a list of resolved IP addresses provided by a discovery service.

## Running ClusterBootstrap
This benchmark is run using Docker and `docker-compose`.

First, build all of the benchmark images by running the following command at the root of the repository directory:

```
PS> ./build.cmd docker
```

In the [`/src/ClusterBootstrap/docker`](docker/) folder you can deploy the sample by running the following command:

```
PS> docker-compose up
```

This will launch a size 3-node cluster.