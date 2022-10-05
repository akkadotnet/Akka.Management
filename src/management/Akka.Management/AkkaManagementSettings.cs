//-----------------------------------------------------------------------
// <copyright file="AkkaManagementSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Akka.Configuration;

namespace Akka.Management
{
    public class AkkaManagementSettings
    {
        public static AkkaManagementSettings Create(Config config)
            => new AkkaManagementSettings(Http.Create(config));

        internal AkkaManagementSettings(Http http)
        {
            Http = http;
        }

        public Http Http { get; }
    }

    public class Http
    {
        public static Http Create(Config config)
        {
            var cc = config.GetConfig("akka.management.http");

            var port = cc.GetInt("port");
            var bindPort = !int.TryParse(cc.GetString("bind-port"), out var effectiveBindPort) ? port : effectiveBindPort;
            
            static bool IsValidFqcn(object value) => value != null && !string.IsNullOrWhiteSpace(value.ToString()) && value.ToString() != "null";

            var routeProviders = cc.GetConfig("routes").AsEnumerable()
                .Where(pair => IsValidFqcn(pair.Value.GetString()))
                .Select(pair => new NamedRouteProvider(pair.Key, pair.Value.GetString()));

            return new Http(
                cc.GetString("hostname"),
                port,
                cc.GetString("bind-hostname"),
                bindPort,
                cc.GetString("base-path"),
                routeProviders,
                cc.GetBoolean("route-providers-read-only"));
        }
        
        private Http(
            string hostname,
            int port,
            string effectiveBindHostname,
            int effectiveBindPort,
            string basePath,
            IEnumerable<NamedRouteProvider> routeProviders,
            bool routeProvidersReadOnly)
        {
            Hostname = hostname;
            if (string.IsNullOrWhiteSpace(Hostname) || Hostname.Equals("<hostname>"))
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                Hostname = addresses.First(ip => !Equals(ip, IPAddress.Any) && !Equals(ip, IPAddress.IPv6Any))
                    .ToString();
            }

            Port = port;
            if (Port < 0 || Port > 65535)
                throw new ArgumentException($"akka.management.http.port must be 0 through 65535 (was {Port})");

            EffectiveBindHostname = !string.IsNullOrEmpty(effectiveBindHostname) ? effectiveBindHostname : Hostname;
            
            EffectiveBindPort = effectiveBindPort;
            if (EffectiveBindPort < 0 || EffectiveBindPort > 65535)
                throw new ArgumentException($"akka.management.http.bind-port must be 0 through 65535 (was {EffectiveBindPort})");

            BasePath = basePath;
            RouteProviders = routeProviders.ToImmutableList();
            RouteProvidersReadOnly = routeProvidersReadOnly;
        }

        public string Hostname { get; }

        public int Port { get; }

        public string EffectiveBindHostname { get; }

        public int EffectiveBindPort { get; }

        public string BasePath { get; }

        public ImmutableList<NamedRouteProvider> RouteProviders { get; }

        public bool RouteProvidersReadOnly { get; }

        internal Http Copy(
            string hostname = null,
            int? port = null,
            string effectiveBindHostname = null,
            int? effectiveBindPort = null,
            string basePath = null,
            IEnumerable<NamedRouteProvider> routeProviders = null,
            bool? routeProvidersReadOnly = null)
            => new Http(
                hostname: hostname ?? Hostname,
                port: port ?? Port,
                effectiveBindHostname: effectiveBindHostname ?? EffectiveBindHostname,
                effectiveBindPort: effectiveBindPort ?? EffectiveBindPort,
                basePath: basePath ?? BasePath,
                routeProviders: routeProviders ?? RouteProviders,
                routeProvidersReadOnly: routeProvidersReadOnly ?? RouteProvidersReadOnly);
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