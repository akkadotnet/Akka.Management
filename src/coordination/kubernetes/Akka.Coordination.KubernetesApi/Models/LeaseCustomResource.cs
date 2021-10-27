//-----------------------------------------------------------------------
// <copyright file="LeaseCustomResource.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using k8s.Models;
using Newtonsoft.Json;

#nullable enable
namespace Akka.Coordination.KubernetesApi.Models
{
    internal class LeaseCustomResource: CustomResource<LeaseSpec>
    {
        [Obsolete(message:"Used only for json deserialization")]
        public LeaseCustomResource()
        { }

        public LeaseCustomResource(
            V1ObjectMeta metadata,
            LeaseSpec spec)
        {
            Metadata = metadata;
            Spec = spec;
            Kind = "Lease";
            ApiVersion = "akka.io/v1";
        }
    }

    internal sealed class LeaseSpec
    {
        [Obsolete(message: "Used only for json deserialization")]
        public LeaseSpec()
        {
        }
        
        public LeaseSpec(string? owner, DateTime time)
        {
            Owner = owner;
            Time = (long) time.TimeOfDay.TotalMilliseconds;
        }
        
        public LeaseSpec(string? owner, long time)
        {
            Owner = owner;
            Time = time;
        }

        [JsonProperty("owner")]
        public string? Owner { get; set; }
        
        [JsonProperty("time")]
        public long Time { get; set; }
    }
}