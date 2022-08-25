// -----------------------------------------------------------------------
//  <copyright file="KubernetesLeaseSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor.Setup;

namespace Akka.Coordination.KubernetesApi
{
    public class KubernetesLeaseSetup: Setup
    {
        public string ApiCaPath { get; set; }
        public string ApiTokenPath { get; set; }
        public string ApiServiceHostEnvName { get; set; }
        public string ApiServicePortEnvName { get; set; }
        public string Namespace { get; set; }
        public string NamespacePath { get; set; }
        public TimeSpan? ApiServiceRequestTimeout { get; set; }
        public bool? Secure { get; set; }
        public TimeSpan? BodyReadTimeout { get; set; }

        internal KubernetesSettings Apply(KubernetesSettings settings)
        {
            if (ApiCaPath != null)
                settings = settings.WithApiCaPath(ApiCaPath);
            if (ApiTokenPath != null)
                settings = settings.WithApiTokenPath(ApiTokenPath);
            if (ApiServiceHostEnvName != null)
                settings = settings.WithApiServiceHostEnvName(ApiServiceHostEnvName);
            if (ApiServicePortEnvName != null)
                settings = settings.WithApiServicePortEnvName(ApiServicePortEnvName);
            if (Namespace != null)
                settings = settings.WithNamespace(Namespace);
            if (NamespacePath != null)
                settings = settings.WithNamespacePath(NamespacePath);
            if (ApiServiceRequestTimeout != null)
                settings = settings.WithApiServiceRequestTimeout(ApiServiceRequestTimeout.Value);
            if (Secure != null)
                settings = settings.WithSecure(Secure.Value);
            if (BodyReadTimeout != null)
                settings = settings.WithBodyReadTimeout(BodyReadTimeout.Value);
            return settings;
        }
        
    }
}