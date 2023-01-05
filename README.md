# Akka Management
This project provides a home for Akka.NET cluster management, bootstrapping, and more.
These tools aims to help with cluster management in various dynamic environments such as Azure, Amazon AWS and Kubernetes.

## Supported Plugins

* [`Akka.Management`](/src/management/Akka.Management) - Akka.Cluster management tool over HTTP. 
  You can read more in the documentation [here](/src/management/Akka.Management/README.md).
* `Akka.Management.Cluster.Bootstrap` - Automated Akka.Cluster bootstrapping
  in a dynamic environment. You can read more in the documentation [here](https://github.com/akkadotnet/Akka.Management/blob/dev/src/management/Akka.Management/README.md#akkamanagementclusterbootstrap).
  > **NOTE**
  >
  > As of version 1.0.0, cluster bootstrap came bundled with the core `Akka.Management` NuGet package and are part of the default HTTP endpoint for `Akka.Management`. All `Akka.Management.Cluster.Bootstrap` NuGet package versions below 1.0.0 should now be considered deprecated.
* [`Akka.Discovery.AwsApi`](/src/discovery/aws/Akka.Discovery.AwsApi) - Akka.Cluster bootstrapping discovery service using EC2, ECS, and the AWS API. You can read more in the documentation [here](https://github.com/akkadotnet/Akka.Management/blob/dev/src/discovery/aws/Akka.Discovery.AwsApi/README.md).
* [`Akka.Discovery.KubernetesApi`](/src/discovery/kubernetes/Akka.Discovery.KubernetesApi) - Akka.Cluster bootstrapping discovery service using Kubernetes API. You can read more in the documentation [here](https://github.com/akkadotnet/Akka.Management/blob/dev/src/discovery/kubernetes/Akka.Discovery.KubernetesApi/README.md).
* [`Akka.Discovery.Azure`](src/discovery/azure/Akka.Discovery.Azure) - Akka.Cluster bootstrapping discovery service using Azure Table Storage. You can read more in the documentation [here](https://github.com/akkadotnet/Akka.Management/blob/dev/src/discovery/azure/Akka.Discovery.Azure/README.md).
* [`Akka.Coordination.KubernetesApi`](https://github.com/akkadotnet/Akka.Management/tree/dev/src/coordination/kubernetes/Akka.Coordination.KubernetesApi) - provides a lease-based distributed lock mechanism backed by [Kubernetes CRD](https://kubernetes.io/docs/tasks/extend-kubernetes/custom-resources/custom-resource-definitions/) for [Akka.NET Split Brain Resolver](https://getakka.net/articles/clustering/split-brain-resolver.html), [Akka.Cluster.Sharding](https://getakka.net/articles/clustering/cluster-sharding.html), and [Akka.Cluster.Singleton](https://getakka.net/articles/clustering/cluster-singleton.html). Documentation can be read [here](https://github.com/akkadotnet/Akka.Management/blob/dev/src/coordination/kubernetes/Akka.Coordination.KubernetesApi/README.md)
* [`Akka.Coordination.Azure`](https://github.com/akkadotnet/Akka.Management/tree/dev/src/coordination/azure/Akka.Coordination.Azure) - provides a lease-based distributed lock mechanism backed by [Microsoft Azure Blob Storage](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blobs-overview) for [Akka.NET Split Brain Resolver](https://getakka.net/articles/clustering/split-brain-resolver.html), [Akka.Cluster.Sharding](https://getakka.net/articles/clustering/cluster-sharding.html), and [Akka.Cluster.Singleton](https://getakka.net/articles/clustering/cluster-singleton.html). Documentation can be read [here](https://github.com/akkadotnet/Akka.Management/blob/dev/src/coordination/azure/Akka.Coordination.Azure/README.md)

## Build Instructions

### Supported Commands
This project supports a wide variety of commands, all of which can be listed via:

**Windows**
```
c:\> build.cmd help
```

**Linux / OS X**
```
c:\> build.sh help
```

However, please see this readme for full details.

#### Summary

* `build.[cmd|sh] all` - runs the entire build system minus documentation: `NBench`, `Tests`, and `Nuget`.
* `build.[cmd|sh] buildrelease` - compiles the solution in `Release` mode.
* `build.[cmd|sh] runtestsnetcore` - compiles the solution in `Release` mode and runs the unit test suite using the netcoreapp3.1 platform (all projects that end with the `.Tests.csproj` suffix). All of the output will be published to the `./TestResults` folder.
* `build.[cmd|sh] runtestsnet` - compiles the solution in `Release` mode and runs the unit test suite using the net5.0 platform (all projects that end with the `.Tests.csproj` suffix). All of the output will be published to the `./TestResults` folder.
* `build.[cmd|sh] nbench` - compiles the solution in `Release` mode and runs the [NBench](https://nbench.io/) performance test suite (all projects that end with the `.Tests.Performance.csproj` suffix). All of the output will be published to the `./PerfResults` folder.
* `build.[cmd|sh] nuget` - compiles the solution in `Release` mode and creates Nuget packages from any project that does not have `<IsPackable>false</IsPackable>` set and uses the version number from `RELEASE_NOTES.md`.
* `build.[cmd|sh] nuget nugetprerelease=dev` - compiles the solution in `Release` mode and creates Nuget packages from any project that does not have `<IsPackable>false</IsPackable>` set - but in this instance all projects will have a `VersionSuffix` of `-beta{DateTime.UtcNow.Ticks}`. It's typically used for publishing nightly releases.
* `build.[cmd|sh] nuget SignClientUser=$(signingUsername) SignClientSecret=$(signingPassword)` - compiles the solution in `Release` modem creates Nuget packages from any project that does not have `<IsPackable>false</IsPackable>` set using the version number from `RELEASE_NOTES.md`, and then signs those packages using the SignClient data below.
* `build.[cmd|sh] nuget SignClientUser=$(signingUsername) SignClientSecret=$(signingPassword) nugetpublishurl=$(nugetUrl) nugetkey=$(nugetKey)` - compiles the solution in `Release` modem creates Nuget packages from any project that does not have `<IsPackable>false</IsPackable>` set using the version number from `RELEASE_NOTES.md`, signs those packages using the SignClient data below, and then publishes those packages to the `$(nugetUrl)` using NuGet key `$(nugetKey)`.
* `build.[cmd|sh] DocFx` - compiles the solution in `Release` mode and then uses [DocFx](http://dotnet.github.io/docfx/) to generate website documentation inside the `./docs/_site` folder. Use the `./serve-docs.cmd` on Windows to preview the documentation.

This build script is powered by [FAKE](https://fake.build/); please see their API documentation should you need to make any changes to the [`build.fsx`](build.fsx) file.
