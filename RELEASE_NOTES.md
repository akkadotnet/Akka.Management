#### 1.5.68 May 31st 2026 ####

* Update to [Akka.NET v1.5.68](https://github.com/akkadotnet/akka.net/releases/tag/1.5.68)
* Update to [Akka.Hosting v1.5.68](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.68)

#### 1.5.67 April 28th 2026 ####

* Update to [Akka.NET v1.5.67](https://github.com/akkadotnet/akka.net/releases/tag/1.5.67)
* Update to [Akka.Hosting v1.5.67](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.67)

#### 1.5.65 April 10th 2026 ####

* Update to [Akka.NET v1.5.65](https://github.com/akkadotnet/akka.net/releases/tag/1.5.65)
* Update to [Akka.Hosting v1.5.65](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.65)

#### 1.5.63 March 31st 2026 ####

* Update to [Akka.NET v1.5.63](https://github.com/akkadotnet/akka.net/releases/tag/1.5.63)
* Update to [Akka.Hosting v1.5.63](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.63)
* [Fix heartbeat self-conflict causes unnecessary lease release after transient timeout](https://github.com/akkadotnet/Akka.Management/pull/3418)
* [Fix intermittent NRE in ServerSettings.Create due to config injection race](https://github.com/akkadotnet/Akka.Management/pull/3419)

#### 1.5.62 March 9th 2026 ####

* Update to [Akka.NET v1.5.62](https://github.com/akkadotnet/akka.net/releases/tag/1.5.62)
* Update to [Akka.Hosting v1.5.62](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.62)
* [fix heartbeat surrender bug](https://github.com/akkadotnet/Akka.Management/pull/3407) - transient heartbeat failure causes immediate lease surrender instead of retrying within the TTL safety window in both Azure and Kubernetes `LeaseActor`.
* [fix premature LeaseAcquired bug](https://github.com/akkadotnet/Akka.Management/pull/3406) - `LeaseActor` sends `LeaseAcquired` before the conflict retry write completes, causing a potential split-brain scenario in both Azure and Kubernetes providers.

#### 1.5.61 February 27th 2026 ####

* Update to [Akka.NET v1.5.61](https://github.com/akkadotnet/akka.net/releases/tag/1.5.61)
* Update to [Akka.Hosting v1.5.61](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.61)
* [Fix `CreateIfNotExistsAsync` bug in `Akka.Coordination.Azure`](https://github.com/akkadotnet/Akka.Management/pull/3398) - works around a known Azure SDK bug ([azure-sdk-for-net#28549](https://github.com/Azure/azure-sdk-for-net/issues/28549)) where `CreateIfNotExistsAsync` could still throw a 409 conflict exception.
* [Update health check guidance in `reference.conf`](https://github.com/akkadotnet/Akka.Management/pull/3395) - corrects outdated comment that pointed users to the deprecated `Akka.HealthCheck` NuGet package.

#### 1.5.60 February 10th 2026 ####

* Update to [Akka.NET v1.5.60](https://github.com/akkadotnet/akka.net/releases/tag/1.5.60)
* Update to [Akka.Hosting v1.5.60](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.60)

#### 1.5.59 January 26th 2026 ####

* Update to [Akka.NET v1.5.59](https://github.com/akkadotnet/akka.net/releases/tag/1.5.59)
* Update to [Akka.Hosting v1.5.59](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.59)

#### 1.5.57 December 16th 2025 ####

* Update to [Akka.NET v1.5.57](https://github.com/akkadotnet/akka.net/releases/tag/1.5.57)
* Update to [Akka.Hosting v1.5.57](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.57)

#### 1.5.55 October 26th 2025 ####

* [Improve logging for Cluster.Bootstrap hostname matching diagnostics](https://github.com/akkadotnet/Akka.Management/pull/3388) - fixes [#3387](https://github.com/akkadotnet/Akka.Management/issues/3387)
* [Update Akka.Hosting and Pbm versions](https://github.com/akkadotnet/Akka.Management/pull/3389)

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