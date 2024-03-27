// -----------------------------------------------------------------------
//  <copyright file="KubernetesDiscoveryOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Text;
using Akka.Actor.Setup;
using Akka.Hosting;

namespace Akka.Discovery.KubernetesApi;

public class KubernetesDiscoveryOptions: IHoconOption
{
    private const string FullPath = "akka.discovery.kubernetes-api";
    
    public string ConfigPath { get; } = "kubernetes-api";
    public Type Class { get; } = typeof(KubernetesApiServiceDiscovery);
    
    public string? ApiCaPath { get; set; }
    public string? ApiTokenPath { get; set; }
    public string? ApiServiceHostEnvName { get; set; }
    public string? ApiServicePortEnvName { get; set; }
    public string? PodNamespacePath { get; set; }
    public string? PodNamespace { get; set; }
    public bool? AllNamespaces { get; set; }
    public string? PodDomain { get; set; }
    public string? PodLabelSelector { get; set; }
    public bool? RawIp { get; set; }
    public string? ContainerName { get; set; }
    
    public void Apply(AkkaConfigurationBuilder builder, Setup? setup = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath} {{");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");

        if (ApiCaPath is { })
            sb.AppendLine($"api-ca-path = {ApiCaPath.ToHocon()}");
        if (ApiTokenPath is { })
            sb.AppendLine($"api-token-path = {ApiTokenPath.ToHocon()}");
        if (ApiServiceHostEnvName is { })
            sb.AppendLine($"api-service-host-env-name = {ApiServiceHostEnvName.ToHocon()}");
        if (ApiServicePortEnvName is { })
            sb.AppendLine($"api-service-port-env-name = {ApiServicePortEnvName.ToHocon()}");
        if (PodNamespacePath is { })
            sb.AppendLine($"pod-namespace-path = {PodNamespacePath.ToHocon()}");
        if (PodNamespace is { })
            sb.AppendLine($"pod-namespace = {PodNamespace.ToHocon()}");
        if (AllNamespaces is { })
            sb.AppendLine($"all-namespaces = {AllNamespaces.ToHocon()}");
        if (PodDomain is { })
            sb.AppendLine($"pod-domain = {PodDomain.ToHocon()}");
        if (PodLabelSelector is { })
            sb.AppendLine($"pod-label-selector = {PodLabelSelector.ToHocon()}");
        if (RawIp is { })
            sb.AppendLine($"use-raw-ip = {RawIp.ToHocon()}");
        if (ContainerName is { })
            sb.AppendLine($"container-name = {ContainerName.ToHocon()}");
        
        sb.AppendLine("}");

        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
        builder.AddHocon(KubernetesDiscovery.DefaultConfiguration(), HoconAddMode.Append);
    }
}