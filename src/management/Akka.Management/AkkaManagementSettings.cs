//-----------------------------------------------------------------------
// <copyright file="AkkaManagementSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using Akka.Configuration;

namespace Akka.Management
{
    public class AkkaManagementSettings
    {
        public AkkaManagementSettings(Config config) => 
            Http = new Http(config);

        public Http Http { get; }
    }

    public class Http
    {
        public Http(Config config)
        {
            var managementConfig = config.GetConfig("akka.management");
            var cc = managementConfig.GetConfig("http");

            var hostname = cc.GetString("hostname");
            if (string.IsNullOrWhiteSpace(hostname) || hostname.Equals("<hostname>"))
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                hostname = addresses
                    .First(ip => !Equals(ip, IPAddress.Any) && !Equals(ip, IPAddress.IPv6Any))
                    .ToString();
            }
            Hostname = hostname;

            Port = cc.GetInt("port");
            if (Port < 0 || Port > 65535)
                throw new ArgumentException($"akka.management.http.port must be 0 through 65535 (was {Port})");

            var effectiveBindHostname = cc.GetString("bind-hostname");
            EffectiveBindHostname = !string.IsNullOrEmpty(effectiveBindHostname) ? effectiveBindHostname : Hostname;
            
            EffectiveBindPort = !int.TryParse(cc.GetString("bind-port"), out var effectiveBindPort) ? Port : effectiveBindPort;
            if (EffectiveBindPort < 0 || EffectiveBindPort > 65535)
                throw new ArgumentException($"akka.management.http.bind-port must be 0 through 65535 (was {EffectiveBindPort})");

            BasePath = cc.GetString("base-path");

            static bool IsValidFqcn(object value) => value != null && !string.IsNullOrWhiteSpace(value.ToString()) && value.ToString() != "null";

            RouteProviders = cc.GetConfig("routes").AsEnumerable()
                .Where(pair => IsValidFqcn(pair.Value.GetString()))
                .Select(pair => new NamedRouteProvider(pair.Key, pair.Value.GetString()))
                .ToImmutableList();

            RouteProvidersReadOnly = cc.GetBoolean("route-providers-read-only");
        }

        public string Hostname { get; }

        public int Port { get; }

        public string EffectiveBindHostname { get; }

        public int EffectiveBindPort { get; }

        public string BasePath { get; }

        public ImmutableList<NamedRouteProvider> RouteProviders { get; }

        public bool RouteProvidersReadOnly { get; }
    }

    public sealed class NamedRouteProvider : IEquatable<NamedRouteProvider>
    {
        public string Name { get; }
        public string FullyQualifiedClassName { get; }

        public NamedRouteProvider(string name, string fullyQualifiedClassName)
        {
            Name = name;
            FullyQualifiedClassName = fullyQualifiedClassName;
        }

        public void Deconstruct(out string name, out string fullyQualifiedClassName)
        {
            name = Name;
            fullyQualifiedClassName = FullyQualifiedClassName;
        }

        public bool Equals(NamedRouteProvider other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && FullyQualifiedClassName == other.FullyQualifiedClassName;
        }

        public override bool Equals(object obj) => 
            ReferenceEquals(this, obj) || obj is NamedRouteProvider other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (FullyQualifiedClassName != null ? FullyQualifiedClassName.GetHashCode() : 0);
            }
        }
    }
}