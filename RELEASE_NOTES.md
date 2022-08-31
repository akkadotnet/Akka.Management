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
