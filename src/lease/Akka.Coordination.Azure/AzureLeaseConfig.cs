// -----------------------------------------------------------------------
// <copyright file="AzureLeaseConfig.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Azure.Storage.Blobs.Models;

namespace Akka.Coordination.Azure
{
    /// <summary>
    /// Configuration class for <see cref="AzureLease"/>.
    /// </summary>
    public sealed class AzureLeaseConfig
    {
        internal AzureLeaseConfig(string connectionString, string containerName,
            TimeSpan connectTimeout, TimeSpan requestTimeout, 
            bool autoInitialize, PublicAccessType containerPublicAccessType)
        {
            ConnectionString = connectionString;
            ContainerName = containerName;
            ConnectTimeout = connectTimeout;
            RequestTimeout = requestTimeout;
            AutoInitialize = autoInitialize;
            ContainerPublicAccessType = containerPublicAccessType;
        }

        /// <summary>
        ///     The connection string for connecting to Windows Azure blob storage account.
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        ///     The table of the container we'll be using to serialize these blobs.
        /// </summary>
        public string ContainerName { get; }

        /// <summary>
        ///     Initial timeout to use when connecting to Azure Container Storage for the first time.
        /// </summary>
        public TimeSpan ConnectTimeout { get; }

        /// <summary>
        ///     Timeouts for individual read, write, and delete requests to Azure Container Storage.
        /// </summary>
        public TimeSpan RequestTimeout { get; }

        public bool AutoInitialize { get; }

        public PublicAccessType ContainerPublicAccessType { get; }

        /// <summary>
        ///     Creates an <see cref="AzureLeaseConfig" /> instance using the
        ///     `akka.coordination.lease.azure` HOCON configuration section.
        /// </summary>
        /// <param name="config">The `akka.coordination.lease.azure` HOCON section.</param>
        /// <returns>A new settings instance.</returns>
        public static AzureLeaseConfig Create(Config config)
        {
            var connectionString = config.GetString("connection-string");
            var containerName = config.GetString("container-name");
            var blobName = config.GetString("blob-name");
            var connectTimeout = config.GetTimeSpan("connect-timeout", TimeSpan.FromSeconds(3));
            var requestTimeout = config.GetTimeSpan("request-timeout", TimeSpan.FromSeconds(3));
            var autoInitialize = config.GetBoolean("auto-initialize", true);

            var accessType = config.GetString("container-public-access-type", PublicAccessType.BlobContainer.ToString());

            if (!Enum.TryParse<PublicAccessType>(accessType, true, out var containerPublicAccessType))
                throw new ConfigurationException(
                    "Invalid [container-public-access-type] value. Valid values are 'None', 'Blob', and 'BlobContainer'");

            return new AzureLeaseConfig(
                connectionString,
                containerName,
                connectTimeout,
                requestTimeout,
                autoInitialize,
                containerPublicAccessType);
        }
    }
}