#### 1.5.48 August 29th 2025 ####

**Major Feature: Akka.Discovery.Dns**

This release introduces the new [**Akka.Discovery.Dns** module](https://github.com/akkadotnet/Akka.Management/tree/dev/src/discovery/dns/Akka.Discovery.Dns), providing native DNS-based service discovery for Akka.NET clusters! This powerful addition enables:

* **DNS-based Service Discovery**: Automatically discover Akka.NET nodes using DNS SRV and A records
* **Cloud-Native Integration**: Seamlessly integrate with Kubernetes DNS, Azure DNS, AWS Route 53, and other DNS providers
* **Zero-Configuration Discovery**: Works out-of-the-box with standard DNS infrastructure
* **Cluster Bootstrap Support**: Full integration with Akka.Management cluster bootstrap for automatic cluster formation

The DNS discovery mechanism is ideal for containerized environments, cloud deployments, and any infrastructure with DNS-based service registration.

**Other Improvements:**

* Update to [Akka.NET v1.5.48](https://github.com/akkadotnet/akka.net/releases/tag/1.5.48)
* Update to [Akka.Hosting v1.5.48](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.48)
* [Make AWS ECS Service discovery mechanism public and non-sealed for extensibility](https://github.com/akkadotnet/Akka.Management/pull/3375) - AWS ECS discovery can now be extended and customized
* [Bump Azure.Identity dependencies](https://github.com/akkadotnet/Akka.Management/pull/3373)