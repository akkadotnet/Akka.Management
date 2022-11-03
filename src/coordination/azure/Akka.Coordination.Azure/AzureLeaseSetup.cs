// -----------------------------------------------------------------------
//  <copyright file="AzureLeaseSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Event;
using Azure.Core;
using Azure.Storage.Blobs;

#nullable enable
namespace Akka.Coordination.Azure
{
    public sealed class AzureLeaseSetup : Setup
    {
        public string? ConnectionString { get; set; }
        public string? ContainerName { get; set; }
        public TimeSpan? ApiServiceRequestTimeout { get; set; }
        public TimeSpan? BodyReadTimeout { get; set; }
        public Uri? ServiceEndpoint { get; set; }
        public TokenCredential? AzureCredential { get; set; }
        public BlobClientOptions? BlobClientOptions { get; set; }

        internal AzureLeaseSettings Apply(AzureLeaseSettings settings, ActorSystem? system)
        {
            if (ConnectionString is { })
                settings = settings.WithConnectionString(ConnectionString);

            if (ContainerName is { })
                settings = settings.WithContainerName(ContainerName);

            if (ApiServiceRequestTimeout is { })
                settings = settings.WithApiServiceRequestTimeout(ApiServiceRequestTimeout.Value);

            if (BodyReadTimeout is { })
                settings = settings.WithBodyReadTimeout(BodyReadTimeout.Value);

            if (AzureCredential is { })
            {
                if(ServiceEndpoint is null)
                {
                    if (system is { })
                    {
                        var log = Logging.GetLogger(system, this);
                        log.Error(
                            "Skipping AzureCredential setup. Both AzureCredential and ServiceEndpoint must be defined.");
                    }
                }
                else
                    settings = settings.WithAzureCredential(AzureCredential, ServiceEndpoint);
            }

            if (BlobClientOptions is { })
                settings = settings.WithBlobClientOption(BlobClientOptions);
            
            return settings;
        }
    }
}