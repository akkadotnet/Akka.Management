#### 1.5.52 October 9th 2025 ####

* Update to [Akka.NET v1.5.52](https://github.com/akkadotnet/akka.net/releases/tag/1.5.52)
* Update to [Akka.Hosting v1.5.52](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.52)

#### 1.5.50 September 23rd 2025 ####

* Update to [Akka.NET v1.5.50](https://github.com/akkadotnet/akka.net/releases/tag/1.5.50)
* Update to [Akka.Hosting v1.5.50](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.50)
* [Bump KubernetesClient to 17.0.14](https://github.com/akkadotnet/Akka.Management/pull/3381) 

> [!NOTE]
> We're dropping .NET Standard 2.0 and .NET 6.0 support for all Kubernetes based projects due to [CVE-2025-9708](https://github.com/advisories/GHSA-w7r3-mgwf-4mqq).
> KubernetesClient has been bumped to 17.0.14, which requires all Kubernetes project to only support .NET 8.0