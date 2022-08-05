// -----------------------------------------------------------------------
//  <copyright file="AkkaManagementSetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor.Setup;

namespace Akka.Management
{
    public sealed class AkkaManagementSetup: Setup
    {
        public HttpSetup Http { get; set; }

        internal AkkaManagementSettings Apply(AkkaManagementSettings settings)
        {
            return Http is null ? settings : new AkkaManagementSettings(Http.Apply(settings.Http));
        }
    }

    public sealed class HttpSetup: Setup
    {
        public string Hostname { get; set; }
        public int? Port { get; set; }
        public string EffectiveBindHostname { get; set; }
        public int? EffectiveBindPort { get; set; }
        public string BasePath { get; set; }
        public Dictionary<string, Type> RouteProviders { get; set; } = new Dictionary<string, Type>();
        public bool? RouteProvidersReadOnly { get; set; }

        internal Http Apply(Http settings)
        {
            var routeProviders = RouteProviders != null && RouteProviders.Count > 0
                ? RouteProviders?.Select(kvp => new NamedRouteProvider(kvp.Key, kvp.Value.AssemblyQualifiedName))
                : null;
            
            return settings.Copy(
                hostname: Hostname,
                port: Port,
                effectiveBindHostname: EffectiveBindHostname,
                effectiveBindPort: EffectiveBindPort,
                basePath: BasePath,
                routeProviders: routeProviders,
                routeProvidersReadOnly: RouteProvidersReadOnly);
        }
    }
}