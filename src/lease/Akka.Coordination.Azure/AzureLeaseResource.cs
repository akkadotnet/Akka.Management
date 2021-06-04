// -----------------------------------------------------------------------
// <copyright file="AzureLeaseResource.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Annotations;
using Akka.Util;

namespace Akka.Coordination.Azure
{
    /// <summary>
    /// INTERNAL API
    /// </summary>
    [InternalApi]
    internal sealed class AzureLeaseResource
    {
        public AzureLeaseResource(Option<string> owner, string version, long time)
        {
            Owner = owner;
            Version = version;
            Time = time;
        }

        public Option<string> Owner { get; }
        public string Version { get; }
        public long Time { get; }

        public bool IsTaken => Owner.HasValue;
    }
}