// -----------------------------------------------------------------------
//  <copyright file="IAzureApi.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Util;
using Azure;

namespace Akka.Coordination.Azure
{
    internal interface IAzureApi
    {
        /// <summary>
        /// Reads a Lease from Azure Blob server. If it doesn't exist it tries to create it.
        /// The creation can fail due to another instance creating at the same time, in this case
        /// the read is retried.
        /// </summary>
        /// <param name="name">The name of the lease</param>
        /// <returns></returns>
        Task<LeaseResource> ReadOrCreateLeaseResource(string name);

        /// <summary>
        /// Update the lease resource by uploading an updated blob to the Azure Blob server.
        /// </summary>
        /// <param name="leaseName">The name of the lease</param>
        /// <param name="ownerName">The owner of the lease, this can be empty</param>
        /// <param name="version">The version of this lease</param>
        /// <param name="time">The last update time, defaults to <see cref="DateTimeOffset.UtcNow"/></param>
        /// <returns></returns>
        Task<Either<LeaseResource, LeaseResource>> UpdateLeaseResource(
            string leaseName,
            string ownerName,
            ETag version,
            DateTimeOffset? time = null);
    }
}