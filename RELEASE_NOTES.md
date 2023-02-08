#### 1.0.2 January 30 2023 ####

Version 1.0.1 contains a code cleanup for `Akka.Coordination.Azure` to suppress non-needed noise emitted by lease release and acquire.

* [[Lease.Azure] Fix lease release/acquire operation logic](https://github.com/akkadotnet/Akka.Management/pull/1289)
* Update dependency NuGet package versions
  * [Bump Akka.Hosting from 1.0.1 to 1.0.3](https://github.com/akkadotnet/Akka.Management/pull/1250)
  * [Bump AWSSDK.EC2 from 3.7.115.1 to 3.7.120](https://github.com/akkadotnet/Akka.Management/pull/1270)
  * [Bump AWSSDK.ECS from 3.7.104.15 to 3.7.104.24](https://github.com/akkadotnet/Akka.Management/pull/1273)
  * [Bump AWSSDK.S3 from 3.7.101.60 to 3.7.103.1](https://github.com/akkadotnet/Akka.Management/pull/1250)
  * [Bump AWSSDK.CludFormation from 3.7.102.36 to 3.7.104.5](https://github.com/akkadotnet/Akka.Management/pull/1275)

#### 1.0.1 January 30 2023 ####

Version 1.0.1 contains a patch for `Akka.Coordination.Azure` bug throwing uncaught exceptions.

* Update to [Akka.NET v1.4.49](https://github.com/akkadotnet/akka.net/releases/tag/1.4.49)
* [[Lease.Azure] Fix uncaught exception and implement async-await pattern](https://github.com/akkadotnet/Akka.Management/pull/1256)
* Update dependency NuGet package versions
  * [Bump AWSSDK.S3 from 3.7.101.55 to 3.7.101.60](https://github.com/akkadotnet/Akka.Management/pull/1250)

#### 1.0.0 January 18 2023 ####

This version 1.0.0 release is the RTM release for `Akka.Management`; all public API will be frozen from this point forward and backed by our backward compatibility promise.

* [[Management] Change Hosting extension method argument from Setup to Options](https://github.com/akkadotnet/Akka.Management/pull/1211)
* [[Discovery.AWS] Change Hosting extension method argument from Setup to Options](https://github.com/akkadotnet/Akka.Management/pull/1211)
* [[Discovery.Azure] Change Hosting extension method argument from Setup to Options](https://github.com/akkadotnet/Akka.Management/pull/1205)
* [[Discovery.Kubernetes] Change Hosting extension method argument from Setup to Options](https://github.com/akkadotnet/Akka.Management/pull/1200)
* [Clean up Setup class internals](https://github.com/akkadotnet/Akka.Management/pull/1226)
* Update dependency NuGet package versions
  * [Bump Akka.Hosting from 1.0.0 to 1.0.1](https://github.com/akkadotnet/Akka.Management/pull/1199)
  * [Bump AWSSDK.CloudFormation from 3.7.102.11 to 3.7.102.36](https://github.com/akkadotnet/Akka.Management/pull/1223)
  * [Bump AWSSDK.ECS from 3.7.102.11 to 3.7.104.15](https://github.com/akkadotnet/Akka.Management/pull/1222)
  * [Bump AWSSDK.EC2 from to 3.7.113 to 3.7.115.1](https://github.com/akkadotnet/Akka.Management/pull/1224)
  * [Bump Azure.Identity from 1.8.0 to 1.8.1](https://github.com/akkadotnet/Akka.Management/pull/1217)

All Akka.Hosting extension methods now takes a POCO Options class instead of a Setup class; they can now be bound directly using `Microsoft.Extensions.Configuration` `IConfiguration.Get&lt;T&gt;()` and `IConfiguration.Bind()` methods. 

You can still use Setup classes by using the `Akka.Hosting` `.AddSetup()` extension method.

#### 1.0.0-beta2 January 6 2023 ####

* [Add missing Akka.Http.Shim package](https://github.com/akkadotnet/Akka.Management/pull/1191)

#### 1.0.0-beta1 January 6 2023 ####

* Update to [Akka.NET v1.4.48](https://github.com/akkadotnet/akka.net/releases/tag/1.4.48)
* [Replace original JVM web routing port with Ceen routing](https://github.com/akkadotnet/Akka.Management/pull/1152)
* [Delete health check endpoint and merge cluster bootstrap endpoint to akka.management](https://github.com/akkadotnet/Akka.Management/pull/1053)
* [[Coordination.Kubernetes] Hosting extension now accepts options class and not setup](https://github.com/akkadotnet/Akka.Management/pull/1172)
* [[Coordination.Azure] Hosting extension now accepts options class and not setup](https://github.com/akkadotnet/Akka.Management/pull/1181)
* Update dependency NuGet package versions
  * [Bump Petabridge.Cmd from 1.2.0 to 1.2.1](https://github.com/akkadotnet/Akka.Management/pull/1071)
  * [Bump Akka.Hosting from 0.5.2-beta1 to 1.0.0](https://github.com/akkadotnet/Akka.Management/pull/1154)
  * [Bump AWSSDK.S3 from 3.7.101.30 to 3.7.101.33](https://github.com/akkadotnet/Akka.Management/pull/1076)
  * [Bump AWSSDK.ECS from 3.7.102.8 to 3.7.102.11](https://github.com/akkadotnet/Akka.Management/pull/1077)
  * [Bump AWSSDK.CloudFormation from 3.7.102.9 to 3.7.102.11](https://github.com/akkadotnet/Akka.Management/pull/1074)
  * [Bump AWSSDK.EC2 from 3.7.111.1 to 3.7.113](https://github.com/akkadotnet/Akka.Management/pull/1067)
  * [Bump Google.Protobuf from 3.21.11 to 3.21.12](https://github.com/akkadotnet/Akka.Management/pull/1073)

**API breaking change**

* To make the API consistent with other `Akka.Hosting` plugin ecosystem, `Akka.Coordination.Kubernetes` `WithKubernetesLease()` now takes `KubernetesLeaseOption` as its argument, not `KubernetesLeaseSetup`.
* To make the API consistent with other `Akka.Hosting` plugin ecosystem, `Akka.Coordination.Azure` `WithAzureLease()` now takes `AzureLeaseOption` as its argument, not `AzureLeaseSetup`.
* Health check functions are consolidated into `Akka.HealthCheck`, the default health check endpoint are removed from `Akka.Management`
* `Akka.Management.Cluster.Bootstrap` endpoint is merged into `Akka.Management` and became the default endpoint.

#### 0.3.0-beta4 December 1 2022 ####

Version 0.3.0-beta4 is a minor release that contains some minor bug fixes and NuGet package updates.

* Update to [Akka.NET v1.4.46](https://github.com/akkadotnet/akka.net/releases/tag/1.4.46)
* [[Coordination.Azure] Fix missing container from REST API URI when using `AzureCredential`](https://github.com/akkadotnet/Akka.Management/pull/1063)
* Update dependency NuGet package versions
  * [Bump Akka.Hosting from 0.5.1 to 0.5.2-beta1](https://github.com/akkadotnet/Akka.Management/pull/1054)
  * [Bump Azure.Identity from 1.7.0 to 1.8.0](https://github.com/akkadotnet/Akka.Management/pull/1046)
  * [Bump Azure.Data.Tables from 12.6.1 to 12.7.1](https://github.com/akkadotnet/Akka.Management/pull/957)
  * [Bump AWSSDK.EC2 from 3.7.102.1 to 3.7.111.1](https://github.com/akkadotnet/Akka.Management/pull/1067)
  * [Bump AWSSDK.S3 from 3.7.9.101.8 to 3.7.101.26](https://github.com/akkadotnet/Akka.Management/pull/1065)
  * [Bump AWSSDK.ECS from 3.7.100.8 to 3.7.102.3](https://github.com/akkadotnet/Akka.Management/pull/1064)
  * [Bump AWSSDK.CloudFormation from 3.7.101.4 to 3.7.102.7](https://github.com/akkadotnet/Akka.Management/pull/997)

#### 0.3.0-beta3 November 7 2022 ####

Version 0.3.0-beta3 is a minor release that contains some minor bug fixes.

* [[Coordination.Azure] `AzureLeaseSetup` will log an error if user did not set both `AzureCredential` and `ServiceEndpoint` properties](https://github.com/akkadotnet/Akka.Management/pull/991)
* Update dependency NuGet package versions
  * [Bump Petabridge.Cmd from 1.1.2 to 1.1.3](https://github.com/akkadotnet/Akka.Management/pull/954)
  * [Bump Akka.Hosting from 0.5.0 to 0.5.1](https://github.com/akkadotnet/Akka.Management/pull/958)
  * [Bump AWSSDK.S3 from 3.7.9.65 to 3.7.101.8](https://github.com/akkadotnet/Akka.Management/pull/995)
  * [Bump AWSSDK.ECS from 3.7.5.90 to 3.7.100.8](https://github.com/akkadotnet/Akka.Management/pull/994)
  * [Bump AWSSDK.EC2 from 3.7.95 to 3.7.102.1](https://github.com/akkadotnet/Akka.Management/pull/993)
  * [Bump AWSSDK.CloudFormation from 3.7.11.41 to 3.7.101.4](https://github.com/akkadotnet/Akka.Management/pull/997)
  * [Bump Azure.Storage.Blobs from 12.14.0 to 12.14.1](https://github.com/akkadotnet/Akka.Management/pull/957)
  * [Bump GoogleProtobuf from 3.21.7 to 3.21.9](https://github.com/akkadotnet/Akka.Management/pull/976)

#### 0.3.0-beta2 October 20 2022 ####

Version 0.3.0-beta2 is a minor release that contains some minor bug fixes. 

* Update to [Akka.NET v1.4.45](https://github.com/akkadotnet/akka.net/releases/tag/1.4.45)
* [[Cluster.Bootstrap] Fix wrong fallback port value](https://github.com/akkadotnet/Akka.Management/pull/925)
* [Bump Azure.Storage.Blobs from 12.13.1 to 12.14.0](https://github.com/akkadotnet/Akka.Management/pull/931)

#### 0.3.0-beta1 October 5 2022 ####

Version 0.3.0-beta1 adds `Akka.Coordination.Azure` support, allowing you to use Azure Blob Storage as an Akka Lease backend. It also has a few breaking change to the `Akka.Coordination.KubernetesApi` `Akka.Hosting` support and `Akka.Discovery.Azure` `Akka.Hosting` support.

* Update to [Akka.NET v1.4.43](https://github.com/akkadotnet/akka.net/releases/tag/1.4.43)
* [[Coordination.Azure] Add Akka.Coordination.Azure Akka Lease support](https://github.com/akkadotnet/Akka.Management/pull/865)
* [[Discovery.Aws] Add programmatic Setup class](https://github.com/akkadotnet/Akka.Management/pull/802)
* [[Discovery.Aws] Add Akka.Hosting support](https://github.com/akkadotnet/Akka.Management/pull/818)
* [[Discovery.Aws.Ec2] Add validator to Ec2ServiceDiscoverySetup](https://github.com/akkadotnet/Akka.Management/pull/846)
* [[Discovery.Azure] Refactor `DefaultAzureCredential` to `TokenCredential`](https://github.com/akkadotnet/Akka.Management/pull/892)
* [[Discovery.Kubernetes] Add programmatic Setup class](https://github.com/akkadotnet/Akka.Management/pull/819)
* [[Discovery.Kubernetes] Add Akka.Hosting support](https://github.com/akkadotnet/Akka.Management/pull/822)
* Update dependency NuGet package versions
  * [Bump Akka.Hosting to 0.5.0](https://github.com/akkadotnet/Akka.Management/pull/907)
  * [Bump Azure.Identity to 1.7.0](https://github.com/akkadotnet/Akka.Management/pull/859)
  * [Bump Google.Protobuf to 3.21.7](https://github.com/akkadotnet/Akka.Management/pull/897)
  * [Bump AWSSDK.ECS to 3.7.5.89](https://github.com/akkadotnet/Akka.Management/pull/905)
  * [Bump AWSSDK.EC2 to 3.7.94](https://github.com/akkadotnet/Akka.Management/pull/906)
  * [Bump AWSSDK.S3 to 3.7.9.54](https://github.com/akkadotnet/Akka.Management/pull/855)

To use `Akka.Coordination.KubernetesApi` or `Akka.Coordination.Azure` lease with Akka split-brain resolver, instead of passing in a HOCON path into `LeaseImplementation` property inside the `LeaseMajorityOption` class, you will need to pass in `KubernetesLeaseOption` or `AzureLeaseOption` instance instead.

All `DefaultAzureCredential` parameters and properties has been refactored to its base class `TokenCredential` for better flexibility. 

#### 0.2.5-beta4 August 16 2022 ####

Version 0.2.5-beta4 adds `Akka.Hosting` support to `Akka.Coordination.KubernetesApi`, allowing you to set a Kubernetes based lease lock through `Akka.Hosting`.

* [[Management] Include `Exception` cause inside start-up failure warning](https://github.com/akkadotnet/Akka.Management/pull/779)
* [[Discovery.Azure] Clean-up discovery entry during shutdown](https://github.com/akkadotnet/Akka.Management/pull/780)
* [[Discovery.Azure] Add `DefaultAzureCredential` support](https://github.com/akkadotnet/Akka.Management/pull/778)
* [[Discovery.Azure] Add `TableClientOption` support](https://github.com/akkadotnet/Akka.Management/pull/783)
* [[Coordination.KubernetesApi] Add `ActorSystemSetup` support](https://github.com/akkadotnet/Akka.Management/pull/781)
* [[Coordination.KubernetesApi] Add `Akka.Hosting` support](https://github.com/akkadotnet/Akka.Management/pull/784)
* [[Management] Harden `Akka.Management` and `Cluster.Bootstrap` startup interaction](https://github.com/akkadotnet/Akka.Management/pull/789)
* Update dependency NuGet package versions
  * [Bump Grpc.Tools from 2.47.0 to 2.48.0 (#762)](https://github.com/akkadotnet/Akka.Management/pull/762)
  * [Bump AWSSDK.CloudFormation from 3.7.11.15 to 3.7.11.20](https://github.com/akkadotnet/Akka.Management/pull/793)
  * [Bump AWSSDK.EC2 from 3.7.81.2 to 3.7.83.3](https://github.com/akkadotnet/Akka.Management/pull/799)
  * [Bump AWSSDK.ECS from 3.7.5.63 to 3.7.5.69](https://github.com/akkadotnet/Akka.Management/pull/798)
  * [Bump AWSSDK.S3 from 3.7.9.39 to 3.7.9.45](https://github.com/akkadotnet/Akka.Management/pull/797)

#### 0.2.5-beta3 August 16 2022 ####

Version 0.2.5-beta3 adds `Akka.Hosting` support to `Akka.Management`, `Akka.Management.Cluster.Bootstrap`, and `Akka.Discovery.Azure`, allowing users to configure these modules through `Akka.Hosting`.

* [[Discovery.Azure] Add shutdown cleanup implementation](https://github.com/akkadotnet/Akka.Management/pull/742)
* [[Management] Add Akka.Hosting support](https://github.com/akkadotnet/Akka.Management/pull/747)
* [[ClusterBootstrap] Add Akka.Hosting support](https://github.com/akkadotnet/Akka.Management/pull/747)
* [[Discovery.Azure] Add Akka.Hosting support](https://github.com/akkadotnet/Akka.Management/pull/747)
* Update dependency NuGet package versions
  * [Bump Google.Protobuf from 3.21.4 to 3.21.5](https://github.com/akkadotnet/Akka.Management/pull/748)
  * [Bump AWSSDK.S3 from 3.7.9.36 to 3.7.9.39](https://github.com/akkadotnet/Akka.Management/pull/758)
  * [Bump AWSSDK.EC2 from 3.7.80.2 to 3.7.81.2](https://github.com/akkadotnet/Akka.Management/pull/758)
  * [Bump AWSSDK.ECS from 3.7.5.60 to 3.7.5.63](https://github.com/akkadotnet/Akka.Management/pull/760)
  * [Bump AWSSDK.CloudFormation from 3.7.11.12 to 3.7.11.15](https://github.com/akkadotnet/Akka.Management/pull/759)

#### 0.2.5-beta2 August 9 2022 ####
* [[ClusterBootstrap] Add programmatic setup to ClusterBootstrap](https://github.com/akkadotnet/Akka.Management/pull/730)
* [[Discovery.Azure] Fix OData query bug and AskTimeoutException bug](https://github.com/akkadotnet/Akka.Management/pull/723)
* [[Discovery.Azure] Fix inconsistent IP address resolution in guardian actor](https://github.com/akkadotnet/Akka.Management/pull/728)
* [[Http.Shim] Replace internal HTTP server from Kestrel to Ceen.Httpd](https://github.com/akkadotnet/Akka.Management/pull/729)
* [[Management] Add programmatic setup to Akka.Management](https://github.com/akkadotnet/Akka.Management/pull/731)
* NuGet package version updates:
  * [Update AWSSDK.EC2 from 3.7.79.2 to 3.7.80.2](https://github.com/akkadotnet/Akka.Management/pull/733)
  * [Update AWSSDK.ECS from 3.7.5.57 to 3.7.5.60](https://github.com/akkadotnet/Akka.Management/pull/732)
  * [Update Azure.Data.Tables from 12.6.0 to 12.6.1](https://github.com/akkadotnet/Akka.Management/pull/718)
  * [Update Google.Protobuf from 3.21.2 to 3.21.4](https://github.com/akkadotnet/Akka.Management/pull/724)

#### 0.2.5-beta1 August 1 2022 ####

* Update to [Akka.NET v1.4.40](https://github.com/akkadotnet/akka.net/releases/tag/1.4.40)
* Update AWS SDK versions to latest:
  * [AWSSDK.S3 to 3.7.9.33](https://github.com/akkadotnet/Akka.Management/pull/709)
  * [AWSSDK.EC2 to 3.7.79.2](https://github.com/akkadotnet/Akka.Management/pull/708)
  * [AWSSDK.CloudFormation to 3.7.11.9](https://github.com/akkadotnet/Akka.Management/pull/710)
  * [AWSSDK.ECS to 3.7.5.57](https://github.com/akkadotnet/Akka.Management/pull/711)
* [Update PBM version to 1.1.0](https://github.com/akkadotnet/Akka.Management/pull/631)
* [Add Akka.Discovery.Azure discovery feature](https://github.com/akkadotnet/Akka.Management/pull/716)

__Akka.Discovery.Azure__

This new discovery module leverages Azure Table Storage as a source for Akka.NET cluster discovery and bootstraping. A more complete documentation can be read [here](https://github.com/akkadotnet/Akka.Management/tree/dev/src/discovery/azure/Akka.Discovery.Azure)

#### 0.2.4-beta3 May 5 2022 ####

* [Fix async routing in netcoreapp3.1](https://github.com/akkadotnet/Akka.Management/pull/563)

#### 0.2.4-beta2 April 14 2022 ####

* Update to [Akka.NET v1.4.37](https://github.com/akkadotnet/akka.net/releases/tag/1.4.37)
* [Make Kubernetes discovery error message more verbose](https://github.com/akkadotnet/Akka.Management/pull/518)
* [Add working Kubernetes lease stress test project](https://github.com/akkadotnet/Akka.Management/pull/530)
* Update all AWSSDK versions

#### 0.2.4-beta1 December 3 2021 ####
* Fix [Kubernetes discovery label selector and add documentation](https://github.com/akkadotnet/Akka.Management/pull/168)
* Add [Kubernetes lease feature](https://github.com/akkadotnet/Akka.Management/pull/213)
* Fix [Kubernetes discovery throws NRE on containers with portless IP address](https://github.com/akkadotnet/Akka.Management/pull/230)
* Fix [Cluster.Bootstrap default configuration not injected properly on startup](https://github.com/akkadotnet/Akka.Management/pull/221)
* Update to [Akka.NET v1.4.28](https://github.com/akkadotnet/akka.net/releases/tag/1.4.28) 
* Update all AWSSDK versions

#### 0.2.3-beta2 October 5 2021 ####
* Fix [several minor bugs](https://github.com/akkadotnet/Akka.Management/pull/168) 

#### 0.2.3-beta1 October 4 2021 ####
* Fix Akka.Management [default host name bug](https://github.com/akkadotnet/Akka.Management/pull/156)

#### 0.2.2-beta1 September 29 2021 ####
* Update to [Akka.NET v1.4.26](https://github.com/akkadotnet/akka.net/releases/tag/1.4.26)
* [Added Akka.Discovery.KubernetesApi package](https://github.com/akkadotnet/Akka.Management/pull/145) - as the name implies, it uses the Kubernetes API to query for available pods to act as seed nodes.
* Update all AWSSDK versions

#### 0.2.1-beta4 September 4 2021 ####
* [Fix erroneous failing probe messages after cluster formed](https://github.com/akkadotnet/Akka.Management/pull/79)
* Update all AWSSDK versions

#### 0.2.1-beta3 August 16 2021 ####
* Add [.NET 5.0 support to Akka.Http.Shim](https://github.com/akkadotnet/Akka.Management/pull/29)
* Bump [AWSSDK.ECS from 3.7.2.13 to 3.7.2.27](https://github.com/akkadotnet/Akka.Management/pull/32)
* Bump [AWSSDK.S3 from 3.7.1.13 to 3.7.1.23](https://github.com/akkadotnet/Akka.Management/pull/34)
* Bump [AWSSDK.EC2 from 3.7.16.2 to 3.7.20.5](https://github.com/akkadotnet/Akka.Management/pull/36)

#### 0.2.1-beta2 August 12 2021 ####
* Add [documentation](https://github.com/akkadotnet/Akka.Management/pull/25)

#### 0.2.1-beta1 August 5 2021 ####
* Fix [compatibility for Akka.Discovery.Config](https://github.com/akkadotnet/Akka.Management/pull/20)

#### 0.2.0-beta1 August 5 2021 ####
* Added support for [Akka.Management and Akka.Management.Cluster.Bootstrap](https://github.com/akkadotnet/Akka.Management/pull/13)

#### 0.1.0-beta2 August 3 2021 ####
* Added support for [EC2 Instance Metadata Service based credential](https://github.com/akkadotnet/Akka.Management/pull/14)

#### 0.1.0-beta1 July 16 2021 ####
* First beta release
* Added support for [`Akka.Discovery.AwsApi`](https://github.com/akkadotnet/Akka.Management/blob/dev/src/discovery/Akka.Discovery.AwsApi)
