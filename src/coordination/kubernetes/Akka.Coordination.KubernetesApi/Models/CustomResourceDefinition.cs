using System.Collections.Generic;
using k8s;
using k8s.Models;
using Newtonsoft.Json;

namespace Akka.Coordination.KubernetesApi.Models
{
    internal class CustomResourceDefinition
    {
        public static CustomResourceDefinition Create(string @namespace = null)
            => new CustomResourceDefinition
            {
                Kind = "Lease",
                Group = "akka.io",
                Version = "v1",
                PluralName = "leases",
                Namespace = @namespace ?? "default"
            };
        
        public string Version { get; set; }

        public string Group { get; set; }

        public string PluralName { get; set; }

        public string Kind { get; set; }

        public string Namespace { get; set; }
    }
    
    internal abstract class CustomResource : KubernetesObject
    {
        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; set; }
    }

    internal abstract class CustomResource<TSpec> : CustomResource
    {
        [JsonProperty(PropertyName = "spec")]
        public TSpec Spec { get; set; }
    }

    internal class CustomResourceList<T> : KubernetesObject
        where T : CustomResource
    {
        public V1ListMeta Metadata { get; set; }
        public List<T> Items { get; set; }
    }
}