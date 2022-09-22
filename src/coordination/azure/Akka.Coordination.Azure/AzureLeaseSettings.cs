// -----------------------------------------------------------------------
//  <copyright file="AzureLeaseSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;

#nullable enable
namespace Akka.Coordination.Azure
{
    public class AzureLeaseSettings
    {
        public static readonly AzureLeaseSettings Empty = new AzureLeaseSettings(
            connectionString: "",
            containerName: "akka-coordination-lease",
            apiServiceRequestTimeout: TimeSpan.FromSeconds(2),
            bodyReadTimeout: TimeSpan.FromSeconds(1),
            serviceEndpoint: null,
            azureCredential: null,
            blobClientOptions: null); 
        
        public AzureLeaseSettings(
            string connectionString,
            string containerName,
            TimeSpan apiServiceRequestTimeout,
            TimeSpan? bodyReadTimeout,
            Uri? serviceEndpoint,
            DefaultAzureCredential? azureCredential,
            BlobClientOptions? blobClientOptions)
        {
            ConnectionString = connectionString;
            ContainerName = containerName;
            ApiServiceRequestTimeout = apiServiceRequestTimeout;
            BodyReadTimeout = bodyReadTimeout ?? TimeSpan.FromSeconds(1);
            ServiceEndpoint = serviceEndpoint;
            AzureCredential = azureCredential;
            BlobClientOptions = blobClientOptions;
        }

        public static AzureLeaseSettings Create(ActorSystem system, TimeoutSettings leaseTimeoutSettings)
            => Create(system.Settings.Config, leaseTimeoutSettings);
        
        public static AzureLeaseSettings Create(Config rootConfig, TimeoutSettings leaseTimeoutSettings)
        {
            var config = rootConfig.GetConfig(AzureLease.ConfigPath);
            
            var requestTimeoutValue = config.GetStringIfDefined("api-service-request-timeout");
            var apiServerRequestTimeout = !string.IsNullOrWhiteSpace(requestTimeoutValue)
                ? config.GetTimeSpan("api-service-request-timeout")
                : new TimeSpan(leaseTimeoutSettings.OperationTimeout.Ticks * 2 / 5);  // 2/5 gives two API operations + a buffer

            if (apiServerRequestTimeout >= leaseTimeoutSettings.OperationTimeout)
                throw new ConfigurationException(
                    "'api-service-request-timeout can not be less than 'akka.coordination.lease.lease-operation-timeout'");
            
            return new AzureLeaseSettings(
                connectionString: config.GetStringIfDefined("connection-string"),
                containerName: config.GetStringIfDefined("container-name"),
                apiServiceRequestTimeout: apiServerRequestTimeout,
                bodyReadTimeout: new TimeSpan(apiServerRequestTimeout.Ticks / 2),
                serviceEndpoint: null,
                azureCredential: null,
                blobClientOptions: null
            );
        } 
        
        public string ConnectionString { get; }
        public string ContainerName { get; }
        public TimeSpan ApiServiceRequestTimeout { get; }
        public TimeSpan BodyReadTimeout { get; }
        public Uri? ServiceEndpoint { get; }
        public DefaultAzureCredential? AzureCredential { get; }
        public BlobClientOptions? BlobClientOptions { get; }
 
        public AzureLeaseSettings WithConnectionString(string connectionString)
            => Copy(connectionString: connectionString);
        public AzureLeaseSettings WithContainerName(string containerName)
            => Copy(containerName: containerName);
        public AzureLeaseSettings WithApiServiceRequestTimeout(TimeSpan apiServiceRequestTimeout)
            => Copy(apiServiceRequestTimeout: apiServiceRequestTimeout);
        public AzureLeaseSettings WithBodyReadTimeout(TimeSpan bodyReadTimeout)
            => Copy(bodyReadTimeout: bodyReadTimeout);
        public AzureLeaseSettings WithServiceEndpoint(Uri serviceEndpoint)
            => Copy(serviceEndpoint: serviceEndpoint);
        public AzureLeaseSettings WithAzureCredential(DefaultAzureCredential azureCredential)
            => Copy(azureCredential: azureCredential);
        public AzureLeaseSettings WithBlobClientOption(BlobClientOptions blobClientOptions)
            => Copy(blobClientOptions: blobClientOptions);
        
        private AzureLeaseSettings Copy(
            string? connectionString = null,
            string? containerName = null,
            TimeSpan? apiServiceRequestTimeout = null,
            TimeSpan? bodyReadTimeout = null,
            Uri? serviceEndpoint = null,
            DefaultAzureCredential? azureCredential = null,
            BlobClientOptions? blobClientOptions = null)
            => new AzureLeaseSettings(
                connectionString: connectionString ?? ConnectionString,
                containerName: containerName ?? ContainerName,
                apiServiceRequestTimeout: apiServiceRequestTimeout ?? ApiServiceRequestTimeout,
                bodyReadTimeout: bodyReadTimeout ?? BodyReadTimeout,
                serviceEndpoint: serviceEndpoint ?? ServiceEndpoint,
                azureCredential: azureCredential ?? AzureCredential,
                blobClientOptions: blobClientOptions ?? BlobClientOptions);

    }
}