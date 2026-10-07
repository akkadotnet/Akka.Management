#### 1.6.0-beta3 October 7th 2026 ####

* Built against [Akka.NET v1.6.0-beta3](https://github.com/akkadotnet/akka.net/releases/tag/1.6.0-beta3). Akka.Hosting, Akka.Cluster.Hosting and Akka.Remote.Hosting now ship from the akka.net repository at the same version as Akka.NET.
* **Breaking: every package now targets `net10.0` only.** The `netstandard2.0`, `net6.0` and `net8.0` targets are gone. Apps on .NET Framework, .NET 6 or .NET 8, or still on Akka.NET 1.5, should stay on the 1.5.x releases (maintained on the `v1.5` branch).
* **Breaking: requires Akka.NET 1.6.** These packages don't work with Akka.NET 1.5 assemblies; read the [Akka.NET v1.6 breaking changes](https://github.com/akkadotnet/akka.net/blob/dev/BREAKING_CHANGES_V1.6.md) before upgrading.
* `Microsoft.Extensions.*` dependencies move to 10.x and `Google.Protobuf` to 3.36.1, matching Akka.NET 1.6.
* Akka.Management and Cluster Bootstrap now auto-start when Akka.Hosting 1.6 loads them through an `ExtensionsSetup` (`WithAkkaManagement`, `WithClusterBootstrap`, `WithExtension<T>`), as well as from the `akka.extensions` HOCON list. Akka.Hosting 1.6 no longer writes startup extensions into `akka.extensions`.
* Akka.Discovery.Dns moved off the removed `Akka.IO.ByteString` type.
* Removed the direct `OpenTelemetry.Api` dependency that pinned a patched version; Akka.Hosting 1.6 already requires a patched OpenTelemetry.

#### 1.5.73 October 5th 2026 ####

* Update to [Akka.NET v1.5.73](https://github.com/akkadotnet/akka.net/releases/tag/1.5.73)
* Update to [Akka.Hosting v1.5.73](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.73)
* Update to the 1.5.72/1.5.73 [Akka.TestKit.Xunit `IAsyncLifetime` breaking change](https://github.com/akkadotnet/akka.net/pull/8545) — `TestKit` now implements `InitializeAsync`/`DisposeAsync` directly. Derived test specs that declare their own must mark them `override` and chain to the base method, or the build fails with `CS0114`. Updated the affected Azure and Redis test specs accordingly. ([#3479](https://github.com/akkadotnet/Akka.Management/pull/3479))

#### 1.5.70 August 18th 2026 ####

* Update to [Akka.NET v1.5.70](https://github.com/akkadotnet/akka.net/releases/tag/1.5.70)
* Update to [Akka.Hosting v1.5.70](https://github.com/akkadotnet/Akka.Hosting/releases/tag/1.5.70)
* **New package: [`Akka.Discovery.Redis`](https://www.nuget.org/packages/Akka.Discovery.Redis)** — Redis-based service discovery for Akka.Cluster.Bootstrap. Each node registers under a TTL key and refreshes it on a heartbeat; dead nodes expire automatically (no separate pruning process) and stale entries are filtered out of lookups. A lightweight alternative to `Akka.Discovery.Azure`, well suited to Redis-backed environments and .NET Aspire local development. ([#3444](https://github.com/akkadotnet/Akka.Management/pull/3444))
* **New: .NET Aspire integration** — [`Akka.Aspire.Hosting`](https://www.nuget.org/packages/Akka.Aspire.Hosting) (AppHost side) and [`Akka.Aspire`](https://www.nuget.org/packages/Akka.Aspire) (service side) bring automated Akka.NET cluster formation to [.NET Aspire](https://learn.microsoft.com/dotnet/aspire). Declare the discovery backend and replica count in your AppHost, and each service replica auto-discovers its peers, forms a cluster, and reports readiness through a health check — no manual HOCON. ([#3445](https://github.com/akkadotnet/Akka.Management/pull/3445))

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