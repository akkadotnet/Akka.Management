//-----------------------------------------------------------------------
// <copyright file="LeaseCustomResourceDefinition.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Newtonsoft.Json;

#nullable enable
namespace Akka.Coordination.KubernetesApi.Models
{
    internal class LeaseCustomResourceDefinition
    {
        public static LeaseCustomResourceDefinition Create(string? @namespace = null)
            => new LeaseCustomResourceDefinition(@namespace);

        [Obsolete(message:"Used for deserialization")]
        public LeaseCustomResourceDefinition()
        { }

        private LeaseCustomResourceDefinition(string? @namespace = null)
        {
            Namespace = @namespace ?? "default";
        }

        public string Version => "v1";

        public string Group => "akka.io";

        public string PluralName => "leases";

        public string Kind => "Lease";

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        // ReSharper disable once MemberCanBePrivate.Global
        public string Namespace { get; set; } = "default";
    }
    
    internal abstract class CustomResource : KubernetesObject
    {
        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; set; } = null!;
    }

    internal abstract class CustomResource<TSpec> : CustomResource
    {
        [JsonProperty(PropertyName = "spec")]
        public TSpec Spec { get; set; } = default!;
    }

    internal class CustomResourceList<T> : KubernetesObject
        where T : CustomResource
    {
        public V1ListMeta Metadata { get; set; } = null!;
        public List<T> Items { get; set; } = null!;
    }
}