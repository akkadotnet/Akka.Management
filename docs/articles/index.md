---
uid: index
title: Akka.Management Documentation
---
# Akka Management
This project provides a home for Akka.NET cluster management, bootstrapping, and more. These tools aims to help with cluster management in various dynamic environments such as Amazon AWS and Kubernetes.

## Supported Plugins

* `Akka.Management` - Akka.Cluster management tool over HTTP.
* `Akka.Management.Cluster.Bootstrap` - Automated Akka.Cluster bootstrapping
  in a dynamic environment.

### Akka Coordination Plugins

* `Akka.Coordination.KubernetesApi` - Akka lease service for Kubernetes

### Akka Discovery Plugins

* `Akka.Discovery.AwsApi` - Akka.Cluster bootstrapping discovery service using EC2 and the AWS API.
* `Akka.Discovery.KubernetesApi` - Akka.Cluster bootstrapping discovery service using Kubernetes API.
