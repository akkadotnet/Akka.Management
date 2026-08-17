// -----------------------------------------------------------------------
//  <copyright file="ClusterMember.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Net;
using System.Text.Json.Serialization;

namespace Akka.Discovery.Redis
{
    /// <summary>
    /// Data model for Redis cluster member entry
    /// </summary>
    internal class ClusterMember
    {
        /// <summary>
        /// Creates a new cluster member instance
        /// </summary>
        [JsonConstructor]
        public ClusterMember(string serviceName, string host, int port, DateTime created, DateTime lastUpdate)
        {
            ServiceName = serviceName;
            Host = host;
            Port = port;
            Created = created;
            LastUpdate = lastUpdate;
        }

        /// <summary>
        /// The service name
        /// </summary>
        [JsonPropertyName("serviceName")]
        public string ServiceName { get; }

        /// <summary>
        /// The hostname or IP address
        /// </summary>
        [JsonPropertyName("host")]
        public string Host { get; }

        /// <summary>
        /// The port number
        /// </summary>
        [JsonPropertyName("port")]
        public int Port { get; }

        /// <summary>
        /// When this member was created
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime Created { get; }

        /// <summary>
        /// When this member was last updated
        /// </summary>
        [JsonPropertyName("lastUpdate")]
        public DateTime LastUpdate { get; }

        /// <summary>
        /// Gets the IP address if Host is a valid IP, otherwise null
        /// </summary>
        [JsonIgnore]
        public IPAddress? Address => IPAddress.TryParse(Host, out var addr) ? addr : null;

        /// <summary>
        /// Creates a new cluster member entry
        /// </summary>
        public static ClusterMember CreateEntity(string serviceName, string host, int port)
        {
            var now = DateTime.UtcNow;
            return new ClusterMember(serviceName, host, port, now, now);
        }

        /// <summary>
        /// Creates a copy with an updated LastUpdate timestamp
        /// </summary>
        public ClusterMember Update()
        {
            return new ClusterMember(ServiceName, Host, Port, Created, DateTime.UtcNow);
        }

        /// <summary>
        /// Creates the Redis key for this member
        /// </summary>
        public static string CreateKey(string keyPrefix, string serviceName, string host, int port)
            => $"{keyPrefix}:{serviceName}:{host}:{port}";

        /// <inheritdoc/>
        public override string ToString()
            => $"[ClusterMember] ServiceName: {ServiceName}, Host: {Host}, Port: {Port}, Created: {Created}, LastUpdate: {LastUpdate}";
    }
}
