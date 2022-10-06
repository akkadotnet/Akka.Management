//-----------------------------------------------------------------------
// <copyright file="KubernetesDiscoverySettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using Akka.Actor;

#nullable enable
namespace Akka.Discovery.KubernetesApi
{
    public sealed class KubernetesDiscoverySettings
    {
        public static readonly KubernetesDiscoverySettings Empty =
            Create(KubernetesDiscovery.DefaultConfiguration().GetConfig("akka.discovery.kubernetes-api"));
        
        public static KubernetesDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config.GetConfig("akka.discovery.kubernetes-api"));

        public static KubernetesDiscoverySettings Create(Configuration.Config config)
            => new KubernetesDiscoverySettings(
                config.GetString("api-ca-path"),
                config.GetString("api-token-path"),
                config.GetString("api-service-host-env-name"),
                config.GetString("api-service-port-env-name"),
                config.GetString("pod-namespace-path"),
                config.GetStringIfDefined("pod-namespace"),
                config.GetString("pod-domain"),
                config.GetString("pod-label-selector"),
                config.GetBoolean("use-raw-ip"),
                config.GetString("container-name")
            );
        
        private readonly string _podLabelSelector;
        
        private KubernetesDiscoverySettings(
            string apiCaPath,
            string apiTokenPath,
            string apiServiceHostEnvName,
            string apiServicePortEnvName,
            string podNamespacePath,
            string podNamespace,
            string podDomain,
            string podLabelSelector,
            bool rawIp,
            string? containerName)
        {
            ApiCaPath = apiCaPath;
            ApiTokenPath = apiTokenPath;
            ApiServiceHostEnvName = apiServiceHostEnvName;
            ApiServicePortEnvName = apiServicePortEnvName;
            PodNamespacePath = podNamespacePath;
            PodNamespace = podNamespace;
            PodDomain = podDomain;
            _podLabelSelector = podLabelSelector;
            RawIp = rawIp;
            ContainerName = string.IsNullOrWhiteSpace(containerName) ? null : containerName;
        }
        
        public string ApiCaPath { get; }
        public string ApiTokenPath { get; }
        public string ApiServiceHostEnvName { get; }
        public string ApiServicePortEnvName { get; }
        public string PodNamespacePath { get; }
        public string PodNamespace { get; }
        public string PodDomain { get; }
        public string PodLabelSelector(string name)
            => string.Format(_podLabelSelector, name);
        public bool RawIp { get; }
        public string? ContainerName { get; }

        public KubernetesDiscoverySettings WithApiCaPath(string apiCaPath)
            => Copy(apiCaPath: apiCaPath);
        public KubernetesDiscoverySettings WithApiTokenPath(string apiTokenPath)
            => Copy(apiTokenPath: apiTokenPath);
        public KubernetesDiscoverySettings WithApiServiceHostEnvName(string apiServiceHostEnvName)
            => Copy(apiServiceHostEnvName: apiServiceHostEnvName);
        public KubernetesDiscoverySettings WithApiServicePortEnvName(string apiServicePortEnvName)
            => Copy(apiServicePortEnvName: apiServicePortEnvName);
        public KubernetesDiscoverySettings WithPodNamespacePath(string podNamespacePath)
            => Copy(podNamespacePath: podNamespacePath);
        public KubernetesDiscoverySettings WithPodNamespace(string podNamespace)
            => Copy(podNamespace: podNamespace);
        public KubernetesDiscoverySettings WithPodDomain(string podDomain)
            => Copy(podDomain: podDomain);
        public KubernetesDiscoverySettings WithPodLabelSelector(string podLabelSelector)
            => Copy(podLabelSelector: podLabelSelector);
        public KubernetesDiscoverySettings WithRawIp(bool rawIp)
            => Copy(rawIp: rawIp);
        public KubernetesDiscoverySettings WithContainerName(string containerName)
            => Copy(containerName: containerName);
        
        internal KubernetesDiscoverySettings Copy(
            string? apiCaPath = null,
            string? apiTokenPath = null,
            string? apiServiceHostEnvName = null,
            string? apiServicePortEnvName = null,
            string? podNamespacePath = null,
            string? podNamespace = null,
            string? podDomain = null,
            string? podLabelSelector = null,
            bool? rawIp = null,
            string? containerName = null)
            => new KubernetesDiscoverySettings(
                apiCaPath: apiCaPath?? ApiCaPath,
                apiTokenPath: apiTokenPath?? ApiTokenPath,
                apiServiceHostEnvName: apiServiceHostEnvName ?? ApiServiceHostEnvName,
                apiServicePortEnvName: apiServicePortEnvName ?? ApiServicePortEnvName,
                podNamespacePath: podNamespacePath ?? PodNamespacePath,
                podNamespace: podNamespace ?? PodNamespace,
                podDomain: podDomain ?? PodDomain,
                podLabelSelector: podLabelSelector ?? _podLabelSelector,
                rawIp: rawIp ?? RawIp,
                containerName: containerName ?? ContainerName
            );
            
        public override string ToString()
            => $"Settings({ApiCaPath}, {ApiTokenPath}, {ApiServiceHostEnvName}, {ApiServicePortEnvName}, " +
               $"{PodNamespacePath}, {PodNamespace}, {PodDomain})";
    }
}