// -----------------------------------------------------------------------
//  <copyright file="AzureLeaseSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor.Setup;
using Azure.Identity;
using Azure.Storage.Blobs;

#nullable enable
namespace Akka.Coordination.Azure
{
    public class AzureLeaseSetup : Setup
    {
        public string? ConnectionString { get; set; }
        public string? ContainerName { get; set; }
        public TimeSpan? ApiServiceRequestTimeout { get; set; }
        public TimeSpan? BodyReadTimeout { get; set; }
        public Uri? ServiceEndpoint { get; set; }
        public DefaultAzureCredential? AzureCredential { get; set; }
        public BlobClientOptions? BlobClientOptions { get; set; }

        internal AzureLeaseSettings Apply(AzureLeaseSettings settings)
        {
            if (ConnectionString is { })
                settings = settings.WithConnectionString(ConnectionString);

            if (ContainerName is { })
                settings = settings.WithContainerName(ContainerName);

            if (ApiServiceRequestTimeout is { })
                settings = settings.WithApiServiceRequestTimeout(ApiServiceRequestTimeout.Value);

            if (BodyReadTimeout is { })
                settings = settings.WithBodyReadTimeout(BodyReadTimeout.Value);

            if (ServiceEndpoint is { })
                settings = settings.WithServiceEndpoint(ServiceEndpoint);

            if (AzureCredential is { })
                settings = settings.WithAzureCredential(AzureCredential);

            if (BlobClientOptions is { })
                settings = settings.WithBlobClientOption(BlobClientOptions);
            
            return settings;
        }
    }
}