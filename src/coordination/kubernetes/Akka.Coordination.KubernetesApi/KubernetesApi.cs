//-----------------------------------------------------------------------
// <copyright file="KubernetesApi.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Util;

#nullable enable
namespace Akka.Coordination.KubernetesApi
{
    internal interface IKubernetesApi
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
            string version,
            DateTime? time = null);
    }

    internal sealed class LeaseResource
    {
        public LeaseResource(string? owner, string version, long time)
        {
            Owner = owner;
            Version = version;
            Time = time;
        }

        public string? Owner { get; }
        public string Version { get; }
        public long Time { get; }
        
        public bool IsTaken => Owner != null;
    }
    
}