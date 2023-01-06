// -----------------------------------------------------------------------
//  <copyright file="AzureLeaseOption.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;
using Akka.Actor.Setup;
using Akka.Cluster.Hosting.SBR;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Hosting.Coordination;
using Azure.Core;
using Azure.Storage.Blobs;

namespace Akka.Coordination.Azure
{
    public class AzureLeaseOption: LeaseOptionBase
    {
        public string? ConnectionString { get; set; }
        public string? ContainerName { get; set; }
        public TimeSpan? ApiServiceRequestTimeout { get; set; }
        public Uri? ServiceEndpoint { get; set; }
        public TokenCredential? AzureCredential { get; set; }
        public BlobClientOptions? BlobClientOptions { get; set; }
        public TimeSpan? HeartbeatInterval { get; set; }
        public TimeSpan? HeartbeatTimeout { get; set; }
        public TimeSpan? LeaseOperationTimeout { get; set; }

        public override string ConfigPath { get; } = "akka.coordination.lease.azure";
        public override Type Class { get; } = typeof(AzureLease);
        
        public override void Apply(AkkaConfigurationBuilder builder, Setup? s = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{ConfigPath} {{");
            sb.AppendLine($"lease-class = {Class.AssemblyQualifiedName!.ToHocon()}");
            if (ConnectionString is { })
                sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
            if (ContainerName is { })
                sb.AppendLine($"container-name = {ContainerName.ToHocon()}");
            if (ApiServiceRequestTimeout is { })
                sb.AppendLine($"api-service-request-timeout = {ApiServiceRequestTimeout.ToHocon()}");
            if (HeartbeatInterval is { })
                sb.AppendLine($"heartbeat-interval = {HeartbeatInterval.ToHocon()}");
            if (HeartbeatTimeout is { })
                sb.AppendLine($"heartbeat-timeout = {HeartbeatTimeout.ToHocon()}");
            if (LeaseOperationTimeout is { })
                sb.AppendLine($"lease-operation-timeout = {LeaseOperationTimeout.ToHocon()}");
            sb.AppendLine("}");

            if ((ServiceEndpoint is { } && AzureCredential is null) ||
                (ServiceEndpoint is null && AzureCredential is { }))
                throw new ConfigurationException("To use AzureCredential, both AzureCredential and ServiceEndpoint need to be populated.");

            builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);

            var setup = new AzureLeaseSetup
            {
                AzureCredential = AzureCredential,
                ServiceEndpoint = ServiceEndpoint,
                BlobClientOptions = BlobClientOptions,
                ApiServiceRequestTimeout = ApiServiceRequestTimeout,
                ConnectionString = ConnectionString,
                ContainerName = ContainerName
            };

            builder.AddSetup(setup);
        }
    }
}