# ClusterSplitter
ClusterSplitter - Induce a split-brain condition in a cluster created using docker-compose

## SYNOPSIS
`ClusterSplitter CLUSTER NETWORK COMMAND`

## DESCRIPTION
Create a split-brain condition in the `CLUSTER` docker cluster inside the `NETWORK` docker network.

This tool assumes that the docker container names would be in the form of `"{CLUSTER}-{NUMBER}"`

COMMAND is a string in the form of

```
<int> [FROM <int> [<int>]...] [AND <int> [FROM] <int> [<int>]...]...
```

- `<int>` is the docker container name `NUMBER` in the `"{CLUSTER}-{NUMBER}"` pattern.
- If `FROM` is not omitted, then the listed node(s) will be split from the list of nodes in the `FROM` section.
- If `FROM` is omitted, then the listed node(s) will be split from the rest of the nodes in the cluster.

## EXAMPLE

```
ClusterSplitter cluster docker_default "3"
```
Splits the node `cluster-3` from the rest of the nodes

```
ClusterSplitter cluster docker_default "3 4 5"
```
Splits node `cluster-3`, `cluster-4`, and `cluster-5` from the rest of the nodes, forming their own island of nodes.

```
ClusterSplitter cluster docker_default "3 FROM 4 5"
```
Splits the node `cluster-3` from `cluster-4` and `cluster-5`

```
ClusterSplitter cluster docker_default "3 AND 10 FROM 4 5"
```
Splits the node `cluster-3` from the rest of the nodes AND splits the node `cluster-10` from node `cluster-4` and `cluster-5`

## NOTE
The cluster service WILL NEED to have NET_ADMIN capability enabled inside the docker-compose.yaml file.
Example docker-compose.yaml file:

```yaml
version: '3'

services:
  cluster:
    image: azure.stresstest:0.2.4
    cap_add:
      - NET_ADMIN
```