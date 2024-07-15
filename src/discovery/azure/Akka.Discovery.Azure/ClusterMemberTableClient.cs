﻿// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberTableClient.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Discovery.Azure.Model;
using Akka.Event;
using Azure;
using Azure.Data.Tables;

#nullable enable
namespace Akka.Discovery.Azure
{
    internal class ClusterMemberTableClient
    {
        private readonly ILoggingAdapter _log;
        private readonly TableClient _client;
        private readonly string _serviceName;
        private bool _initialized;
        private ClusterMember? _entity;

        public ClusterMemberTableClient(
            AzureDiscoverySettings settings,
            ILoggingAdapter log)
        {
            _log = log;
            _serviceName = settings.ServiceName;
            _client = (settings.AzureAzureCredential != null && settings.AzureTableEndpoint != null)
                ? new TableClient(settings.AzureTableEndpoint, settings.TableName, settings.AzureAzureCredential, settings.TableClientOptions)
                : new TableClient(settings.ConnectionString, settings.TableName, settings.TableClientOptions);
        }

        /// <summary>
        /// Try and retrieve the entity entry for the node:
        ///   - If one is found, it will refresh the LastUpdate value and update the table row
        ///   - if none are found, it will insert a new one into the table.
        /// </summary>
        /// <param name="host">The public Akka.Management host name of this node</param>
        /// <param name="address">The public Akka.Management IP address of this node</param>
        /// <param name="port">the public Akka.Management port of this node</param>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns>The immutable Azure <see cref="ClusterMember"/> entity entry of this node</returns>
        /// <exception cref="InitializationException">Failed to initialize the client, could not connect to Azure services</exception>
        /// <exception cref="RequestFailedException">The Azure server returned an error</exception>
        /// <exception cref="UpdateOperationException">Entity update operation failed, Azure service responded with an error</exception>
        /// <exception cref="CreateEntityFailedException">Client failed to insert new entity row, Azure service responded with an error</exception>
        public async ValueTask<ClusterMember> GetOrCreateAsync(
            string? host,
            IPAddress? address,
            int port,
            CancellationToken token = default)
        {
            if (_entity != null)
                return _entity;

            await EnsureInitializedAsync(token);

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
                throw new CreateEntityFailedException(
                    $"[{_serviceName}@{address}:{port}] Failed to insert entry row. " +
                    $"Reason: {response.ReasonPhrase}");
            }

            _entity = ClusterMember.FromEntity(entity);
            if(_log.IsDebugEnabled)
                _log.Debug($"[{_serviceName}:{_entity}] New entry row created.");
            return _entity;
        }

        /// <summary>
        /// Query the Azure table for all entries that has its LastUpdate value greater than or equal to the
        /// <paramref name="lastUpdate"/> parameter.
        /// </summary>
        /// <param name="lastUpdate">The last update tick value to be considered</param>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns>
        /// All cluster member entries that has their LastUpdate value greater than <paramref name="lastUpdate"/>
        /// </returns>
        /// <exception cref="InitializationException">Failed to initialize the client, could not connect to Azure services</exception>
        /// <exception cref="RequestFailedException">The Azure server returned an error</exception>
        public async Task<ImmutableList<ClusterMember>> GetAllAsync(long lastUpdate, CancellationToken token = default)
        {
            await EnsureInitializedAsync(token);

            var query = _client
                .QueryAsync<TableEntity>($"PartitionKey eq '{_serviceName}' and {ClusterMember.LastUpdateName} ge {lastUpdate}L")
                .WithCancellation(token);

            var list = ImmutableList.CreateBuilder<ClusterMember>();
            await foreach (var entry in query)
            {
                list.Add(ClusterMember.FromEntity(entry));
            }
            
            if(_log.IsDebugEnabled)
                _log.Debug($"[{_entity}] Retrieved {list.Count} entry rows.");
            return list.ToImmutableList();
        }

        /// <summary>
        /// Refresh the LastUpdate value to DateTime.UtcNow and updates the table entity row
        /// </summary>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns><c>true</c> if the operation succeeded</returns>
        /// <exception cref="InvalidOperationException">Method was called before the actor was initialized</exception>
        /// <exception cref="UpdateOperationException">Update operation failed, Azure service responded with an error</exception>
        public async Task UpdateAsync(CancellationToken token = default)
        {
            if (_entity is null)
                throw new InvalidOperationException("Invalid update operation, actor has not been initialized");
                    
            var original = _entity.LastUpdate;
            _entity = _entity.Update();
            var response = await _client.UpdateEntityAsync(_entity.Raw, ETag.All, TableUpdateMode.Replace, token);
            if (response.IsError)
            {
                throw new UpdateOperationException(
                    $"[{_serviceName}@{_entity.Address}:{_entity.Port}] Failed to update entity. " +
                    $"Reason: {response.ReasonPhrase}");
            }
            
            if(_log.IsDebugEnabled)
                _log.Debug($"[{_serviceName}@{_entity.Address}:{_entity.Port}] LastUpdate successfully updated " +
                           $"from [{original}] to [{_entity.LastUpdate}]");
        }

        /// <summary>
        /// Removes all entity rows that has their LastUpdate value less than the <paramref name="lastUpdate"/> parameter.
        /// </summary>
        /// <param name="lastUpdate">The last update tick value to be considered</param>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns><c>true</c> if the operation succeeded</returns>
        /// <exception cref="InitializationException">Failed to initialize the client, could not connect to Azure services</exception>
        /// <exception cref="PruneOperationException">At least one of the delete operations returned an error</exception>
        public async Task PruneAsync(long lastUpdate, CancellationToken token = default)
        {
            await EnsureInitializedAsync(token);

            var query = _client
                .QueryAsync<TableEntity>($"PartitionKey eq '{_serviceName}' and {ClusterMember.LastUpdateName} lt {lastUpdate}L")
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
                return;
            }

            var responses = await _client.SubmitTransactionAsync(batch, token);
            var errors = responses.Value
                .Where(r => r.IsError)
                .Select(r => r.ReasonPhrase)
                .ToList();
            
            if(errors.Count == 0)
            {
                if (_log.IsDebugEnabled)
                {
                    var sb = new StringBuilder().AppendLine($"[{_serviceName}] {batch.Count} row entries pruned:");
                    foreach (var item in batch)
                    {
                        sb.AppendLine($"    {ClusterMember.FromEntity((TableEntity)item.Entity)}");
                    }
                    _log.Debug(sb.ToString());
                }
            }
            else
            {
                throw new PruneOperationException($"[{_serviceName}] Failed to prune row entry.", errors);
            }
        }

        /// <summary>
        /// Remove table entity row identifying this node
        /// </summary>
        /// <param name="token"><see cref="CancellationToken"/> to cancel this operation</param>
        /// <exception cref="InvalidOperationException">Thrown when the method is called before the actor was initialized</exception>
        public async Task RemoveSelf(CancellationToken token = default)
        {
            if (_entity is null)
                throw new InvalidOperationException("Invalid remove operation, actor has not been initialized");
                
            await _client.DeleteEntityAsync(_entity.PartitionKey, _entity.RowKey, ETag.All, token);
        }

        #region Helper methods

        /// <summary>
        /// Ensure that the required Azure table exists in the database
        /// </summary>
        /// <param name="token">CancellationToken to cancel this operation</param>
        /// <returns><c>true</c> if the operation succeeded</returns>
        /// <exception cref="InitializationException">Failed to initialize the client, could not connect to Azure services</exception>
        private async Task EnsureInitializedAsync(CancellationToken token)
        {
            if (_initialized)
                return;

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
            }
            catch (RequestFailedException e)
            {
                throw new InitializationException($"[{_serviceName}] Failed to create Azure table {_client.Name}", e);
            }
        }

        /// <summary>
        /// Retrieve a single entity row with RowKey matching <paramref name="rowKey"/>
        /// </summary>
        /// <param name="rowKey">The row RowKey</param>
        /// <param name="token"></param>
        /// <returns><see cref="ClusterMember"/> retrieved</returns>
        /// <exception cref="RequestFailedException">The Azure server returned an error</exception>
        public async Task<ClusterMember?> GetEntityAsync(string rowKey, CancellationToken token)
        {
            var query = _client
                .QueryAsync<TableEntity>($"PartitionKey eq '{_serviceName}' and RowKey eq '{rowKey}'")
                .WithCancellation(token);

            // this is similar to ".FirstOrDefault()" Linq function. We're deliberately NOT using Linq because the
            // bcl NuGet package has a very severe backward target framework compatibility problem.
            await foreach (var entry in query)
            {
                return ClusterMember.FromEntity(entry);
            }

            return default;
        }

        #endregion
    }
}