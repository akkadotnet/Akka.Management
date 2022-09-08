// -----------------------------------------------------------------------
//  <copyright file="KubernetesDiscoverySetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor.Setup;

#nullable enable
namespace Akka.Discovery.KubernetesApi
{
    public class KubernetesDiscoverySetup: Setup
    {
        public string? ApiCaPath { get; set; }
        public string? ApiTokenPath { get; set; }
        public string? ApiServiceHostEnvName { get; set; }
        public string? ApiServicePortEnvName { get; set; }
        public string? PodNamespacePath { get; set; }
        public string? PodNamespace { get; set; }
        public string? PodDomain { get; set; }
        public string? PodLabelSelector { get; set; }
        public bool? RawIp { get; set; }
        public string? ContainerName { get; set; }

        internal KubernetesDiscoverySettings Apply(KubernetesDiscoverySettings settings)
            => settings.Copy(
                apiCaPath: ApiCaPath,
                apiTokenPath: ApiTokenPath,
                apiServiceHostEnvName: ApiServiceHostEnvName,
                apiServicePortEnvName: ApiServicePortEnvName,
                podNamespacePath: PodNamespacePath,
                podNamespace: PodNamespace,
                podDomain: PodDomain,
                podLabelSelector: PodLabelSelector,
                rawIp: RawIp,
                containerName: ContainerName);

    }
}