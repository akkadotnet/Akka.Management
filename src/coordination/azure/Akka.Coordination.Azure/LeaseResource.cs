// -----------------------------------------------------------------------
//  <copyright file="LeaseResource.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Azure;
using Newtonsoft.Json;

#nullable enable
namespace Akka.Coordination.Azure
{
    internal sealed class LeaseBody : IEquatable<LeaseBody>
    {
        [JsonConstructor]
        public LeaseBody(string owner = "", DateTimeOffset? time = null)
        {
            Owner = owner;
            Time = time ?? DateTimeOffset.UtcNow;
        }

        public string Owner { get; }
        public DateTimeOffset Time { get; }

        public bool Equals(LeaseBody? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Owner == other.Owner && Time.Equals(other.Time);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is LeaseBody other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Owner.GetHashCode() * 397) ^ Time.GetHashCode();
            }
        }
    }
    
    internal sealed class LeaseResource : IEquatable<LeaseResource>
    {
        public ETag Version { get; }
        public string? Owner { get; }
        public DateTimeOffset Time { get; }

        public LeaseResource(string owner, ETag version, DateTimeOffset time)
        {
            Owner = string.IsNullOrEmpty(owner) ? null : owner;
            Version = version;
            Time = time;
        }
        
        public LeaseResource(LeaseBody body, ETag version)
        {
            Version = version;

            Owner = string.IsNullOrEmpty(body.Owner) ? null : body.Owner;
            Time = body.Time;
        }


        public override string ToString()
            => $"[LeaseResource] Owner: {Owner}, Version: {Version}, Time: {Time}";

        public bool Equals(LeaseResource? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Version.Equals(other.Version) && Owner == other.Owner && Time.Equals(other.Time);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is LeaseResource other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Version.GetHashCode();
                if(Owner is { })
                    hashCode = (hashCode * 397) ^ Owner.GetHashCode();
                hashCode = (hashCode * 397) ^ Time.GetHashCode();
                return hashCode;
            }
        }
    }
}
