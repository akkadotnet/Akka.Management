// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberEntity.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Azure;
using Azure.Data.Tables;
using Google.Protobuf;

namespace Akka.Discovery.Azure.Model
{
    /// <summary>
    /// Data model for Azure table. We're deliberately not using Azure driver automatic de/serialization by
    /// not inheriting from TableEntity.
    /// Note that only properties that are needed for a TTL implementation is implemented as properties,
    /// any informational data are packed as byte array using protobuf serialization instead. This is intentionally
    /// done so that all data model extensions can only be done using an extend only design through protobuf versioning.
    /// </summary>
    public class ClusterMember: IEquatable<ClusterMember>
    {
        internal const string PayloadName = "payload";
        internal const string LastUpdateName = "lastUpdate";
        
        // If anything throws InvalidOperationException, then the data is corrupted
        public ClusterMember(TableEntity entity)
        {
            PartitionKey = entity.PartitionKey;
            RowKey = entity.RowKey;
            Timestamp = entity.Timestamp;
            ETag = entity.ETag;

            Raw = entity;
            Proto = ClusterMemberProto.Parser.ParseFrom(entity.GetBinary(PayloadName));
            LastUpdate = new DateTime(entity.GetInt64(LastUpdateName).Value);
        }
        
        #region Required fields
        public string PartitionKey { get; }
        public string RowKey { get; }
        public DateTimeOffset? Timestamp { get; }
        public ETag ETag { get; }
        #endregion

        internal TableEntity Raw { get; }
        internal ClusterMemberProto Proto { get; }
        
        public string ServiceName => PartitionKey;
        public string Host => Proto.Host;
        public IPAddress Address => IPAddress.Parse(Proto.Address);
        public int Port => Proto.Port;
        public DateTime Created => Proto.Created.ToDateTime();
        public DateTime LastUpdate { get; }

        public ClusterMember Update()
            => Update(DateTime.UtcNow.Ticks);
        
        internal ClusterMember Update(long ticks)
        {
            var clone = new TableEntity(Raw);
            clone[LastUpdateName] = ticks;
            return new ClusterMember(clone);
        }
        
        public static TableEntity CreateEntity(
            string serviceName,
            string host,
            IPAddress address,
            int port)
        {
            var now = DateTime.UtcNow;
            var proto = new ClusterMemberProto
            {
                Host = host,
                Address = address.MapToIPv4().ToString(),
                Port = port,
                Created = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(now),
            };

            return new TableEntity
            {
                PartitionKey = serviceName,
                RowKey = CreateRowKey(host, address, port),
                // Timestamp and ETag isn't written because it is handled by the system
                [PayloadName] = proto.ToByteArray(),
                [LastUpdateName] = now.Ticks
            };
        }

        public static ClusterMember FromEntity(TableEntity entity)
            => entity != null ? new ClusterMember(entity) : null;
        
        internal static string CreateRowKey(string host, IPAddress address, int port)
            => $"{host}-{address.MapToIPv4()}-{port}";

        internal static (string, IPAddress, int) ParseRowKey(string rowKey)
        {
            var parts = rowKey.Split('-');
            if (parts.Length != 3)
                throw new InvalidOperationException($"RowKey needs to be in [{{Host}}-{{Address}}-{{Port}}] format. was: [{rowKey}]");
            return (parts[0], IPAddress.Parse(parts[1]), int.Parse(parts[2]));
        }

        public bool Equals(ClusterMember other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return 
                PartitionKey == other.PartitionKey && 
                RowKey == other.RowKey &&
                Host == other.Host &&
                Equals(Address, other.Address) &&
                Port == other.Port &&
                Created == other.Created &&
                LastUpdate.Equals(other.LastUpdate);
        }

        public override bool Equals(object obj)
            => obj is ClusterMember entity && Equals(entity);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (PartitionKey != null ? PartitionKey.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RowKey != null ? RowKey.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ LastUpdate.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
            => $"[ClusterMember:{ServiceName}@{Address}:{Port}] Host: {Host}, Created: {Created}, Last update: {LastUpdate}";
    }
}