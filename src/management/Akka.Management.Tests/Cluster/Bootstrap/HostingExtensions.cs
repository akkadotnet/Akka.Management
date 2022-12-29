// -----------------------------------------------------------------------
//  <copyright file="HostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Hosting;

namespace Akka.Management.Tests.Cluster.Bootstrap
{
    public static class HostingExtensions
    {
        public static AkkaConfigurationBuilder WithConfigDiscovery(
            this AkkaConfigurationBuilder builder,
            Dictionary<string, List<string>> services)
        {
            var sb = new StringBuilder();
            foreach (var service in services)
            {
                sb.AppendLine($@"
{service.Key} {{
    endpoints = [ {string.Join(", ", service.Value.Select(s => $"\"{s}\""))} ]
}}
");
            }
            var config = ConfigurationFactory.ParseString($@"
akka.discovery{{
    method = config
    config {{
        services {{
            {sb}
        }}
    }}
}}").WithFallback(DiscoveryProvider.DefaultConfiguration());
        
            builder.AddHocon(config, HoconAddMode.Prepend);
            return builder;
        }

    }
}