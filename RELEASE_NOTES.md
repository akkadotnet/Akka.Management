#### 1.5.27 July 30 2024 ####

* Update to [Akka.NET v1.5.27.1](https://github.com/akkadotnet/akka.net/releases/tag/1.5.27.1)
* Update to [Akka.Hosting v1.5.27](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.27)
* [Discovery: Refactor IHoconOptions to IDiscoveryOptions](https://github.com/akkadotnet/Akka.Management/pull/2678)

All discovery module option classes now implements `IDiscoveryOptions` interface and can be used as an argument of the new `Akka.Cluster.Hosting` `WithClusterClientDiscovery()` method.

#### 1.5.26.1 July 16 2024 ####

* [Discovery.Azure: Revert `AkkaDiscoveryOptions` refactor](https://github.com/akkadotnet/Akka.Management/pull/2656)

#### 1.5.26 July 15 2024 ####

* Update to [Akka.NET v1.5.26](https://github.com/akkadotnet/akka.net/releases/tag/1.5.26)
* [Discovery.KubernetesApi: Add multi-config support](https://github.com/akkadotnet/Akka.Management/pull/2596)
* [Discovery.Azure: Add multi-config support](https://github.com/akkadotnet/Akka.Management/pull/2595)
* [Discovery.Azure: Always apply TableClientOptions to TableClient](https://github.com/akkadotnet/Akka.Management/pull/2650)
* [Discovery.Azure: Implement missing IExtension implementation](https://github.com/akkadotnet/Akka.Management/pull/2653)
* [Discovery.Azure: Refactor `AkkaDiscoveryOptions` to `AzureDiscoveryOptions`](https://github.com/akkadotnet/Akka.Management/pull/2654)
* [Discovery.Config: Add multi-config support](https://github.com/akkadotnet/Akka.Management/pull/2604)
* [Discovery.AwsApi: Add multi-config support](https://github.com/akkadotnet/Akka.Management/pull/2651)
* [ClusterBootstrap: Update probe-interval and stale contact point timeout calculation](https://github.com/akkadotnet/Akka.Management/pull/2601)
* [Management: Cluster bootstrap endpoint returns empty seed if it is not part of a cluster](https://github.com/akkadotnet/Akka.Management/pull/2636)
* [Management: Add Remote address and ClusterClientReceptionist actor path endpoint](https://github.com/akkadotnet/Akka.Management/pull/2614)
* [Management: Harden ClusterClientReceptionist actor path endpoint](https://github.com/akkadotnet/Akka.Management/pull/2615)
* Update dependency NuGet package versions to latest versions

~~**Akka.Discovery.Azure AkkaDiscoveryOptions is not obsolete**~~

~~`AkkaDiscoveryOptions` class has been made obsolete because the name was a typo, the correct class to use from now on is `AzureDiscoveryOptions`. The old `AkkaDiscoveryOptions` class is marked with the `Obsolete` attribute so users can migrate to the correct option class in the future.~~

#### 1.5.26-beta3 July 2 2024 ####

* [ClusterBootstrap: Update probe-interval and stale contact point timeout calculation](https://github.com/akkadotnet/Akka.Management/pull/2601)
* [Management: Add Remote address and ClusterClientReceptionist actor path endpoint](https://github.com/akkadotnet/Akka.Management/pull/2614)
* [Management: Harden ClusterClientReceptionist actor path endpoint](https://github.com/akkadotnet/Akka.Management/pull/2615)

#### 1.5.26-beta2 June 28 2024 ####

* [Discovery.Config: Add multi-config support](https://github.com/akkadotnet/Akka.Management/pull/2604)

#### 1.5.26-beta1 June 27 2024 ####

* Update to [Akka.NET v1.5.26](https://github.com/akkadotnet/akka.net/releases/tag/1.5.26)
* [Discovery.KubernetesApi: Add multi-config support](https://github.com/akkadotnet/Akka.Management/pull/2596)
* [Discovery.Azure: Add multi-config support](https://github.com/akkadotnet/Akka.Management/pull/2595)

#### 1.5.25.1 June 25 2024 ####

* [Add stale-contact-point-timeout settings to ClusterBootstrap](https://github.com/akkadotnet/Akka.Management/pull/2589)
* [Bump Akka.Hosting to 1.5.25](https://github.com/akkadotnet/Akka.Management/pull/2572)
* Update dependency NuGet package versions to latest versions 

#### 1.5.25 June 17 2024 ####

* Update to [Akka.NET v1.5.25](https://github.com/akkadotnet/akka.net/releases/tag/1.5.24)

#### 1.5.24 June 11 2024 ####

* Update to [Akka.NET v1.5.24](https://github.com/akkadotnet/akka.net/releases/tag/1.5.24)
* [Fix CVE-2024-21319 and CVE-2022-26907](https://github.com/akkadotnet/Akka.Management/pull/2557)
* [Fix CVE-2018-8292](https://github.com/akkadotnet/Akka.Management/pull/2558)
* Update dependency NuGet package versions to latest versions
  * [Bump Akka.Hosting to 1.5.24](https://github.com/akkadotnet/Akka.Management/pull/2556)
  * [Bump Azure.Storage.Blobs to 12.20.0](https://github.com/akkadotnet/Akka.Management/pull/2501)
  * [Bump AWSSDK.ECS to 3.7.306](https://github.com/akkadotnet/Akka.Management/pull/2552)
  * [Bump Azure.Identity to 1.11.4](https://github.com/akkadotnet/Akka.Management/pull/2545)
  * [Bump AWSSDK.EC2 to 3.7.329.3](https://github.com/akkadotnet/Akka.Management/pull/2553)
  * [Bump AWSSDK.S3 to 3.7.309.2](https://github.com/akkadotnet/Akka.Management/pull/2554)
  * [Bump AWSSDK.CludFormation to 3.7.308.9](https://github.com/akkadotnet/Akka.Management/pull/2555)
  * [Bump Google.Protobuf to 3.27.1](https://github.com/akkadotnet/Akka.Management/pull/2540)
  * [Bump Petabridge.Cmd to 1.4.2](https://github.com/akkadotnet/Akka.Management/pull/2559)

#### 1.5.19 April 17 2024 ####

* Update to [Akka.NET v1.5.19](https://github.com/akkadotnet/akka.net/releases/tag/1.5.19)
* [Discovery.KubernetesApi: Add option to query pods in all namespaces](https://github.com/akkadotnet/Akka.Management/pull/2421)
* [Coordination.KubernetesApi: Change lease expiration calculation to be based on DateTime.Ticks instead of DateTime.TimeOfDay.TotalMilliseconds](https://github.com/akkadotnet/Akka.Management/pull/2474)
* [Coordination.KubernetesApi: Fix KubernetesSettings configuration bug](https://github.com/akkadotnet/Akka.Management/pull/2475)
* [Management: Fix host name IPV6 detection](https://github.com/akkadotnet/Akka.Management/pull/2476)
* Update dependency NuGet package versions to latest versions
  * [Bump Akka.Hosting to 1.5.19](https://github.com/akkadotnet/Akka.Management/pull/2478)
  * [Bump Google.Protobuf to 3.26.1](https://github.com/akkadotnet/Akka.Management/pull/2436)
  * [Bump KubernetesClient to 13.0.26](https://github.com/akkadotnet/Akka.Management/pull/2405)
  * [Bump Petabridge.Cmd to 1.4.1](https://github.com/akkadotnet/Akka.Management/pull/2418)
  * [Bump AWSSDK.S3 to 3.7.307](https://github.com/akkadotnet/Akka.Management/pull/2412)
  * [Bump AWSSDK.CludFormation to 3.7.305.4](https://github.com/akkadotnet/Akka.Management/pull/2430)
  * [Bump AWSSDK.ECS to 3.7.305.21](https://github.com/akkadotnet/Akka.Management/pull/2414)
  * [Bump AWSSDK.EC2 to 3.7.318](https://github.com/akkadotnet/Akka.Management/pull/2417)

**Breaking Change Warning**

**This release introduces a breaking change on how `Akka.Coordination.KubernetesApi` calculates lease expiration.**

If you're upgrading `Akka.Coordination.KubernetesApi` from v1.5.18-beta2 or lower to 1.5.19, do not attempt to do a Kubernetes cluster rolling update. Instead, you will have to down the whole Akka.NET cluster (or scale everything to 0) first, then deploy the newly upgraded nodes.

#### 1.5.18-beta2 March 20 2024 ####

* [Discovery.KubernetesApi: Add option to query pods in all namespaces](https://github.com/akkadotnet/Akka.Management/pull/2421)
* [Bump AWSSDK.CludFormation to 3.7.305.4](https://github.com/akkadotnet/Akka.Management/pull/2430)

#### 1.5.18-beta1 March 20 2024 ####

* Update to [Akka.NET v1.5.18](https://github.com/akkadotnet/akka.net/releases/tag/1.5.18)
* Update dependency NuGet package versions to latest versions
  * [Bump Akka.Hosting to 1.5.18](https://github.com/akkadotnet/Akka.Management/pull/2410)
  * [Bump KubernetesClient to 13.0.26](https://github.com/akkadotnet/Akka.Management/pull/2405)
  * [Bump Petabridge.Cmd to 1.4.1](https://github.com/akkadotnet/Akka.Management/pull/2418)
  * [Bump AWSSDK.S3 to 3.7.307](https://github.com/akkadotnet/Akka.Management/pull/2412)
  * [Bump AWSSDK.CludFormation to 3.7.305.1](https://github.com/akkadotnet/Akka.Management/pull/2416)
  * [Bump AWSSDK.ECS to 3.7.305.21](https://github.com/akkadotnet/Akka.Management/pull/2414)
  * [Bump AWSSDK.EC2 to 3.7.318](https://github.com/akkadotnet/Akka.Management/pull/2417)

#### 1.5.17.1 March 4 2024 ####

* Update to [Akka.NET v1.5.17.1](https://github.com/akkadotnet/akka.net/releases/tag/1.5.17.1)
* [Modernize sample projects](https://github.com/akkadotnet/Akka.Management/pull/2285)
* Update dependency NuGet package versions to latest versions
  * [Bump Akka.Hosting to 1.5.17.1](https://github.com/akkadotnet/Akka.Management/pull/2381)
  * [Bump Petabridge.Cmd to 1.3.3](https://github.com/akkadotnet/Akka.Management/pull/2279)
  * [Bump AWSSDK.CludFormation to 3.7.303.10](https://github.com/akkadotnet/Akka.Management/pull/2373)
  * [Bump AWSSDK.ECS to 3.7.305.16](https://github.com/akkadotnet/Akka.Management/pull/2374)
  * [Bump AWSSDK.S3 to 3.7.305.30](https://github.com/akkadotnet/Akka.Management/pull/2376)
  * [Bump AWSSDK.EC2 to 3.7.315.2](https://github.com/akkadotnet/Akka.Management/pull/2377)
  * [Bump Azure.Data.Tables to 12.8.3](https://github.com/akkadotnet/Akka.Management/pull/2335)
  * [Bump Grpc.Tools to 2.62.0](https://github.com/akkadotnet/Akka.Management/pull/2366)

#### 1.5.15 January 11 2024 ####

* Update to [Akka.NET v1.5.15](https://github.com/akkadotnet/akka.net/releases/tag/1.5.15)
* Update dependency NuGet package versions to latest versions
  * [Bump Akka.Hosting to 1.5.15](https://github.com/akkadotnet/Akka.Management/pull/2271)
  * [Bump AWSSDK.S3 to 3.7.305.9](https://github.com/akkadotnet/Akka.Management/pull/2274)
  * [Bump AWSSDK.ECS to 3.7.304](https://github.com/akkadotnet/Akka.Management/pull/2275)
  * [Bump AWSSDK.CludFormation to 3.7.302.21](https://github.com/akkadotnet/Akka.Management/pull/2277)
  * [Bump AWSSDK.EC2 to 3.7.311](https://github.com/akkadotnet/Akka.Management/pull/2257)
  * [Bump Google.Protobuf to 3.25.2](https://github.com/akkadotnet/Akka.Management/pull/2264)
  * [Bump Azure.Storage.Blobs to 12.19.1](https://github.com/akkadotnet/Akka.Management/pull/2171)
  * [Bump Azure.Identity to 1.10.4](https://github.com/akkadotnet/Akka.Management/pull/2262)
  * [Bump Azure.Data.Tables to 12.8.2](https://github.com/akkadotnet/Akka.Management/pull/2250)

#### 1.5.7 May 23 2023 ####

* Update to [Akka.NET v1.5.7](https://github.com/akkadotnet/akka.net/releases/tag/1.5.7)
* [Add Akka.Discovery.Config support](https://github.com/akkadotnet/Akka.Management/pull/1758)
* Update dependency NuGet package versions to latest versions
  * [Bump Akka.Hosting to 1.5.7](https://github.com/akkadotnet/Akka.Management/pull/1770)
  * [Bump AWSSDK.S3 to 3.7.104.11](https://github.com/akkadotnet/Akka.Management/pull/1734)
  * [Bump AWSSDK.EC2 to 3.7.135.1](https://github.com/akkadotnet/Akka.Management/pull/1769)
  * [Bump AWSSDK.CludFormation to 3.7.105.25](https://github.com/akkadotnet/Akka.Management/pull/1767)
  * [Bump AWSSDK.ECS to 3.7.108.7](https://github.com/akkadotnet/Akka.Management/pull/1768)
  * [Bump Google.Protobuf to 3.23.1](https://github.com/akkadotnet/Akka.Management/pull/1755)
  * [Bump Azure.Storage.Blobs to 12.16.0](https://github.com/akkadotnet/Akka.Management/pull/1594)

#### 1.5.5 May 4 2023 ####

* Update to [Akka.NET v1.5.5](https://github.com/akkadotnet/akka.net/releases/tag/1.5.5)
* Update dependency NuGet package versions to latest versions
  * [Bump Akka.Hosting to 1.5.5](https://github.com/akkadotnet/Akka.Management/pull/1727)
  * [Bump AWSSDK.ECS to 5.7.107.9](https://github.com/akkadotnet/Akka.Management/pull/1724)
  * [Bump AWSSDK.CludFormation to 3.7.105.17](https://github.com/akkadotnet/Akka.Management/pull/1721)
  * [Bump AWSSDK.EC2 to 3.7.133](https://github.com/akkadotnet/Akka.Management/pull/1725)
  * [Bump AWSSDK.S3 to 3.7.104.9](https://github.com/akkadotnet/Akka.Management/pull/1722)
  * [Bump Grpc.Tools to 2.54.0](https://github.com/akkadotnet/Akka.Management/pull/1660)
  * [Bump Google.Protobuf to 3.22.3](https://github.com/akkadotnet/Akka.Management/pull/1613)

#### 1.5.0 March 2 2023 ####

Version 1.5.0 is the RTM release of Akka.Management and Akka.NET v1.5.0 RTM integration.

* Update to [Akka.NET v1.5.0](https://github.com/akkadotnet/akka.net/releases/tag/1.5.0)
* Update dependency NuGet package versions
  * [Bump AWSSDK.S3 from 3.7.103.1 to 3.7.103.9](https://github.com/akkadotnet/Akka.Management/pull/1318)
  * [Bump AWSSDK.CludFormation from 3.7.104.5 to 3.7.104.11](https://github.com/akkadotnet/Akka.Management/pull/1275)
  * [Bump Google.Protobuf from 3.21.12 to 3.22.0](https://github.com/akkadotnet/Akka.Management/pull/1343)
  * [Bump AWSSDK.ECS from 3.7.104.38 to 3.7.105](https://github.com/akkadotnet/Akka.Management/pull/1385)
  * [Bump AWSSDK.EC2 from 3.7.120 to 3.7.123.5](https://github.com/akkadotnet/Akka.Management/pull/1368)
  * [Bump AWSSDK.S3 from 3.7.103.9 to 3.7.103.14](https://github.com/akkadotnet/Akka.Management/pull/1374)
  * [Bump Akka.Hosting from 1.5.0-alpha4 to 1.5.0-beta6](https://github.com/akkadotnet/Akka.Management/pull/1397)
