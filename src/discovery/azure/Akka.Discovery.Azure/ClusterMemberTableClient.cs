// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberTableClient.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Discovery.Azure.Model;
using Akka.Event;
using Azure;
using Azure.Data.Tables;

namespace Akka.Discovery.Azure
{
    internal class ClusterMemberTableClient
    {
        private readonly ILoggingAdapter _log;
        private readonly TableClient _client;
        private readonly string _serviceName;
        private bool _initialized;
        private ClusterMember _entity;

        public ClusterMemberTableClient(string serviceName, string connectionString, string tableName, ILoggingAdapter log)
        {
            _log = log;
            _serviceName = serviceName;
            _client = new TableClient(connectionString, tableName);
        }

        public async ValueTask<ClusterMember> GetOrCreateAsync(
            string host,
            IPAddress address,
            int port,
            CancellationToken token = default)
        {
            if (_entity != null)
                return _entity;

            if (!await EnsureInitializedAsync(token))
                return null;

            var rowKey = ClusterMember.CreateRowKey(host, address, port);
            var entry = await GetEntityAsync(rowKey, token);
            if (entry != null)
            {
                _entity = entry;
                if(_log.IsDebugEnabled)
                    _log.Debug($"[{_serviceName}@{_entity.Address}:{_entity.Port}] Found existing entry row. " +
                               $"Created: [{_entity.Created}], last update: [{_entity.LastUpdate}]");
                await UpdateAsync(token);
                return _entity;
            }

            var entity = ClusterMember.CreateEntity(_serviceName, host, address, port);
            var response = await _client.AddEntityAsync(entity, token);
            if (response.IsError)
            {
                _log.Error($"[{_serviceName}@{address}:{port}] Failed to insert entry row. " +
                           $"Reason: {response.ReasonPhrase}");
                return null;
            }

            _entity = ClusterMember.FromEntity(entity);
            if(_log.IsDebugEnabled)
                _log.Debug($"[{_serviceName}@{_entity.Address}:{_entity.Port}] New entry row created.");
            return _entity;
        }

        public async Task<ImmutableList<ClusterMember>> GetAllAsync(long lastUpdate, CancellationToken token = default)
        {
            if (!await EnsureInitializedAsync(token))
                return null;

            var query = _client
                .QueryAsync<TableEntity>($"PartitionKey eq '{_serviceName}' and {ClusterMember.LastUpdateName} ge {lastUpdate}")
                .WithCancellation(token);

            var list = ImmutableList.CreateBuilder<ClusterMember>();
            await foreach (var entry in query)
            {
                list.Add(ClusterMember.FromEntity(entry));
            }
            
            if(_log.IsDebugEnabled)
                _log.Debug($"[{_serviceName}@{_entity.Address}:{_entity.Port}] Retrieved {list.Count} entry rows.");
            return list.ToImmutableList();
        }

        public async Task<bool> UpdateAsync(CancellationToken token = default)
        {
            if (!await EnsureInitializedAsync(token))
                return false;

            var original = _entity.LastUpdate;
            _entity = _entity.Update();
            var response = await _client.UpdateEntityAsync(_entity.Raw, ETag.All, TableUpdateMode.Replace, token);
            if (response.IsError)
            {
                _log.Error($"[{_serviceName}@{_entity.Address}:{_entity.Port}] Failed to update entity. " +
                           $"Reason: {response.ReasonPhrase}");
                return false;
            }
            
            if(_log.IsDebugEnabled)
                _log.Debug($"[{_serviceName}@{_entity.Address}:{_entity.Port}] LastUpdate successfully updated " +
                           $"from [{original}] to [{_entity.LastUpdate}]");
            return true;
        }

        public async Task<bool> PruneAsync(long lastUpdate, CancellationToken token = default)
        {
            if (!await EnsureInitializedAsync(token))
                return false;

            var query = _client
                .QueryAsync<TableEntity>($"PartitionKey eq '{_serviceName}' and {ClusterMember.LastUpdateName} lt {lastUpdate}")
                .WithCancellation(token);

            var batch = new List<TableTransactionAction>();

            await foreach (var entry in query)
            {
                batch.Add(new TableTransactionAction(TableTransactionActionType.Delete, entry, ETag.All));
            }

            if (batch.Count == 0)
            {
                // Nothing to prune
                if(_log.IsDebugEnabled)
                    _log.Debug($"[{_serviceName}] No row entries are eligible to prune.");
                return true;
            }

            var responses = await _client.SubmitTransactionAsync(batch, token);
            var errored = false;
            foreach (var response in responses.Value)
            {
                if (response.IsError)
                {
                    _log.Error($"[{_serviceName}] Failed to prune row entry. Reason: {response.ReasonPhrase}");
                    errored = true;
                }
            }

            if(!errored && _log.IsDebugEnabled)
            {
                var sb = new StringBuilder().AppendLine($"[{_serviceName}] {batch.Count} row entries pruned:");
                foreach (var item in batch)
                {
                    sb.AppendLine($"    {ClusterMember.FromEntity((TableEntity)item.Entity)}");
                }
                _log.Debug(sb.ToString());
            }
            return !errored;
        }

        #region Helper methods

        private async Task<bool> EnsureInitializedAsync(CancellationToken token)
        {
            if (_initialized)
                return true;

            try
            {
                var result = await _client.CreateIfNotExistsAsync(token);
                if (_log.IsDebugEnabled)
                {
                    _log.Debug(result != null
                        ? $"[{_serviceName}] Azure table {_client.Name} successfully created"
                        : $"[{_serviceName}] Azure table {_client.Name} already existed");
                }
                _initialized = true;
                return true;
            }
            catch (RequestFailedException ex)
            {
                _log.Error(ex, $"[{_serviceName}] Failed to create Azure table {_client.Name}");
                return false;
            }
        }

        public async Task<ClusterMember> GetEntityAsync(string rowKey, CancellationToken token)
        {
            var query = _client
                .QueryAsync<TableEntity>($"PartitionKey eq '{_serviceName}' and RowKey eq '{rowKey}'")
                .WithCancellation(token);

            await foreach (var entry in query)
            {
                return ClusterMember.FromEntity(entry);
            }
            return null;
        }

        #endregion
    }
}