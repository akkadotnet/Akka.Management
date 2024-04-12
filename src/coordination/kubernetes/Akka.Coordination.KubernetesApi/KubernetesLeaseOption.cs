// -----------------------------------------------------------------------
//  <copyright file="KubernetesLeaseOption.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Hosting.Coordination;

#nullable enable
namespace Akka.Coordination.KubernetesApi
{
    public class KubernetesLeaseOption: LeaseOptionBase
    {
        public string? ApiCaPath { get; set; }
        public string? ApiTokenPath { get; set; }
        public string? ApiServiceHostEnvName { get; set; }
        public string? ApiServicePortEnvName { get; set; }
        public string? Namespace { get; set; }
        public string? NamespacePath { get; set; }
        public TimeSpan? ApiServiceRequestTimeout { get; set; }
        public bool? SecureApiServer { get; set; }
        public TimeSpan? HeartbeatInterval { get; set; }
        public TimeSpan? HeartbeatTimeout { get; set; }
        public TimeSpan? LeaseOperationTimeout { get; set; }
        public bool? UseLegacyTimeOfDayTimeout { get; set; }
        
        public override string ConfigPath => KubernetesLease.ConfigPath;
        public override Type Class { get; } = typeof(KubernetesLease);
        
        public override void Apply(AkkaConfigurationBuilder builder, Setup? inputSetup = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{ConfigPath} {{");
            sb.AppendLine($"lease-class = {Class.AssemblyQualifiedName!.ToHocon()}");
            if (ApiCaPath is { })
                sb.AppendLine($"api-ca-path = {ApiCaPath.ToHocon()}");
            if (ApiTokenPath is { })
                sb.AppendLine($"api-token-path = {ApiTokenPath.ToHocon()}");
            if (ApiServiceHostEnvName is { })
                sb.AppendLine($"api-service-host-env-name = {ApiServiceHostEnvName.ToHocon()}");
            if (ApiServicePortEnvName is { })
                sb.AppendLine($"api-service-port-env-name = {ApiServicePortEnvName.ToHocon()}");
            if (NamespacePath is { })
                sb.AppendLine($"namespace-path = {NamespacePath.ToHocon()}");
            if (Namespace is { })
                sb.AppendLine($"namespace = {Namespace.ToHocon()}");
            if (ApiServiceRequestTimeout is { })
                sb.AppendLine($"api-service-request-timeout = {ApiServiceRequestTimeout.ToHocon()}");
            if (SecureApiServer is { })
                sb.AppendLine($"secure-api-server = {SecureApiServer.ToHocon()}");
            if (HeartbeatInterval is { })
                sb.AppendLine($"heartbeat-interval = {HeartbeatInterval.ToHocon()}");
            if (HeartbeatTimeout is { })
                sb.AppendLine($"heartbeat-timeout = {HeartbeatTimeout.ToHocon()}");
            if (LeaseOperationTimeout is { })
                sb.AppendLine($"lease-operation-timeout = {LeaseOperationTimeout.ToHocon()}");
            if (UseLegacyTimeOfDayTimeout is not null)
                sb.Append($"use-legacy-day-of-time-timeout = {UseLegacyTimeOfDayTimeout.ToHocon()}");
            sb.AppendLine("}");

            //var config = ConfigurationFactory.ParseString(sb.ToString())
            //    .WithFallback(LeaseProvider.DefaultConfig().GetConfig("akka.coordination.lease"));

            builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
        }
    }
}