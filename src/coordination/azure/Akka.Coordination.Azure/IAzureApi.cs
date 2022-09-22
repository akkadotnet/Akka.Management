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
        /// Reads a Lease from the API server. If it doesn't exist it tries to create it.
        /// The creation can fail due to another instance creating at the same time, in this case
        /// the read is retried.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Task<LeaseResource> ReadOrCreateLeaseResource(string name);

        Task<Either<LeaseResource, LeaseResource>> UpdateLeaseResource(
            string leaseName,
            string ownerName,
            ETag version,
            DateTimeOffset? time = null);
    }
}