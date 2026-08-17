// -----------------------------------------------------------------------
//  <copyright file="ClusterMember.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Net;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Akka.Discovery.Redis
{
    /// <summary>
    /// Data model for a Redis cluster member entry.
    /// The record is persisted as protobuf bytes (see protobuf/ClusterMemberProto.proto) rather than
    /// reflection-based JSON, so the wire format follows an extend-only schema. This matches the
    /// contract used by Akka.Discovery.Azure and keeps stored entries forward/backward compatible
    /// across mixed-version rolling upgrades.
    /// </summary>
    internal class ClusterMember
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private ClusterMember(ClusterMemberProto proto)
        {
            Proto = proto;
        }

        /// <summary>
        /// The underlying protobuf message.
        /// </summary>
        internal ClusterMemberProto Proto { get; }

        /// <summary>
        /// The service name.
        /// </summary>
        public string ServiceName => Proto.ServiceName;

        /// <summary>
        /// The hostname or IP address.
        /// </summary>
        public string Host => Proto.Host;

        /// <summary>
        /// The port number.
        /// </summary>
        public int Port => Proto.Port;

        /// <summary>
        /// When this member was created. Defaults to the Unix epoch if the field is absent.
        /// </summary>
        public DateTime Created => Proto.Created?.ToDateTime() ?? Epoch;

        /// <summary>
        /// When this member was last updated. Defaults to the Unix epoch if the field is absent
        /// (which then reads as maximally stale).
        /// </summary>
        public DateTime LastUpdate => Proto.LastUpdate?.ToDateTime() ?? Epoch;

        /// <summary>
        /// Gets the IP address if <see cref="Host"/> is a valid IP, otherwise null.
        /// </summary>
        public IPAddress? Address => IPAddress.TryParse(Host, out var addr) ? addr : null;

        /// <summary>
        /// Creates a new cluster member entry.
        /// </summary>
        public static ClusterMember CreateEntity(string serviceName, string host, int port)
        {
            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            return new ClusterMember(new ClusterMemberProto
            {
                ServiceName = serviceName,
                Host = host,
                Port = port,
                Created = now,
                LastUpdate = now
            });
        }

        /// <summary>
        /// Creates a copy with an updated LastUpdate timestamp.
        /// </summary>
        public ClusterMember Update()
        {
            var clone = Proto.Clone();
            clone.LastUpdate = Timestamp.FromDateTime(DateTime.UtcNow);
            return new ClusterMember(clone);
        }

        /// <summary>
        /// Serializes to the protobuf wire format stored as the Redis value.
        /// </summary>
        public byte[] ToBytes() => Proto.ToByteArray();

        /// <summary>
        /// Parses from the protobuf wire format stored as the Redis value.
        /// </summary>
        public static ClusterMember FromBytes(byte[] bytes) => new(ClusterMemberProto.Parser.ParseFrom(bytes));

        /// <summary>
        /// Creates the Redis key for this member.
        /// </summary>
        public static string CreateKey(string keyPrefix, string serviceName, string host, int port)
            => $"{keyPrefix}:{serviceName}:{host}:{port}";

        /// <inheritdoc/>
        public override string ToString()
            => $"[ClusterMember] ServiceName: {ServiceName}, Host: {Host}, Port: {Port}, Created: {Created}, LastUpdate: {LastUpdate}";
    }
}
