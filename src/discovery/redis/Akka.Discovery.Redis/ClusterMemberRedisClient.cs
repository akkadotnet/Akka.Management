// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberRedisClient.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Akka.Event;
using StackExchange.Redis;

namespace Akka.Discovery.Redis
{
    /// <summary>
    /// Internal client for managing Redis operations for cluster member discovery.
    /// Entries are stored as protobuf bytes (see <see cref="ClusterMember.ToBytes"/>).
    /// </summary>
    internal class ClusterMemberRedisClient
    {
        private readonly ILoggingAdapter _log;
        private readonly IConnectionMultiplexer _connection;
        private readonly IDatabase _database;
        private readonly RedisDiscoverySettings _settings;
        private ClusterMember? _entity;
        private string? _myKey;

        /// <summary>
        /// Creates a new Redis client for cluster member operations
        /// </summary>
        public ClusterMemberRedisClient(
            IConnectionMultiplexer connection,
            RedisDiscoverySettings settings,
            ILoggingAdapter log)
        {
            _connection = connection;
            _database = connection.GetDatabase();
            _settings = settings;
            _log = log;
        }

        /// <summary>
        /// Try and retrieve the entity entry for the node:
        /// - If one is found, it will refresh the LastUpdate value and update Redis
        /// - if none are found, it will insert a new one into Redis
        /// </summary>
        public async ValueTask<ClusterMember> GetOrCreateAsync(CancellationToken token = default)
        {
            if (_entity != null)
                return _entity;

            var host = _settings.HostName;
            var port = _settings.Port;
            var serviceName = _settings.ServiceName;

            _myKey = ClusterMember.CreateKey(_settings.KeyPrefix, serviceName, host, port);

            var existing = await _database.StringGetAsync(_myKey);
            if (existing.HasValue)
            {
                _entity = ClusterMember.FromBytes((byte[])existing!);
                if (_log.IsDebugEnabled)
                    _log.Debug($"[{serviceName}@{host}:{port}] Found existing entry. Created: [{_entity.Created}], last update: [{_entity.LastUpdate}]");
                await UpdateAsync(token);
                return _entity;
            }

            _entity = ClusterMember.CreateEntity(serviceName, host, port);
            await _database.StringSetAsync(_myKey, _entity.ToBytes(), _settings.Ttl);

            if (_log.IsDebugEnabled)
                _log.Debug($"[{serviceName}:{_entity}] New entry created.");

            return _entity;
        }

        /// <summary>
        /// Query Redis for all non-stale cluster member entries for this service.
        /// Entries whose <see cref="ClusterMember.LastUpdate"/> is older than <paramref name="staleThreshold"/>
        /// are excluded, so dead nodes drop out of lookups before their key physically expires.
        /// </summary>
        public async Task<ImmutableList<ClusterMember>> GetAllAsync(TimeSpan staleThreshold, CancellationToken token = default)
        {
            var pattern = $"{_settings.KeyPrefix}:{_settings.ServiceName}:*";
            var members = new List<ClusterMember>();
            var oldestAllowed = DateTime.UtcNow - staleThreshold;

            var endpoints = _connection.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _connection.GetServer(endpoint);
                if (server.IsReplica)
                    continue;

                await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(token))
                {
                    token.ThrowIfCancellationRequested();

                    var value = await _database.StringGetAsync(key);
                    if (!value.HasValue)
                        continue;

                    var member = ClusterMember.FromBytes((byte[])value!);

                    if (member.LastUpdate < oldestAllowed)
                    {
                        if (_log.IsDebugEnabled)
                            _log.Debug($"Skipping stale entry [{member}], last update [{member.LastUpdate}] older than [{oldestAllowed}]");
                        continue;
                    }

                    members.Add(member);
                }
            }

            if (_log.IsDebugEnabled)
                _log.Debug($"[{_entity}] Retrieved {members.Count} entry rows.");

            return members.ToImmutableList();
        }

        /// <summary>
        /// Refresh the LastUpdate value and update Redis with new TTL
        /// </summary>
        public async Task UpdateAsync(CancellationToken token = default)
        {
            if (_entity is null || _myKey is null)
                throw new InvalidOperationException("Invalid update operation, client has not been initialized");

            token.ThrowIfCancellationRequested();

            var original = _entity.LastUpdate;
            _entity = _entity.Update();
            await _database.StringSetAsync(_myKey, _entity.ToBytes(), _settings.Ttl);

            if (_log.IsDebugEnabled)
                _log.Debug($"[{_settings.ServiceName}@{_entity.Host}:{_entity.Port}] LastUpdate successfully updated from [{original}] to [{_entity.LastUpdate}]");
        }

        /// <summary>
        /// Remove the Redis entry for this node
        /// </summary>
        public async Task RemoveSelfAsync(CancellationToken token = default)
        {
            if (_entity is null || _myKey is null)
                throw new InvalidOperationException("Invalid remove operation, client has not been initialized");

            await _database.KeyDeleteAsync(_myKey);

            if (_log.IsDebugEnabled)
                _log.Debug($"[{_settings.ServiceName}@{_entity.Host}:{_entity.Port}] Entry removed from Redis.");
        }
    }
}
