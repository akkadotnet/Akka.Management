using Akka.Actor;

#nullable enable
namespace Akka.Discovery.KubernetesApi
{
    public sealed class KubernetesDiscoverySettings
    {
        private readonly string _podLabelSelector;
        
        public KubernetesDiscoverySettings(ExtendedActorSystem system)
        {
            var kubernetesApi = system.Settings.Config.GetConfig("akka.discovery.kubernetes-api");
            ApiCaPath = kubernetesApi.GetString("api-ca-path");
            ApiTokenPath = kubernetesApi.GetString("api-token-path");
            ApiServiceHostEnvName = kubernetesApi.GetString("api-service-host-env-name");
            ApiServicePortEnvName = kubernetesApi.GetString("api-service-port-env-name");
            PodNamespacePath = kubernetesApi.GetString("pod-namespace-path");
            PodNamespace = kubernetesApi.GetStringIfDefined("pod-namespace");
            PodDomain = kubernetesApi.GetString("pod-domain");
            _podLabelSelector = kubernetesApi.GetString("pod-label-selector");
            RawIp = kubernetesApi.GetBoolean("use-raw-ip");
            var containerName = kubernetesApi.GetString("container-name");
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

        public override string ToString()
            => $"Settings({ApiCaPath}, {ApiTokenPath}, {ApiServiceHostEnvName}, {ApiServicePortEnvName}, " +
               $"{PodNamespacePath}, {PodNamespace}, {PodDomain})";
    }
}