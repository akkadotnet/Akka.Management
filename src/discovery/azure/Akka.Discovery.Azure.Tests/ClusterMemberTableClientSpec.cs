// -----------------------------------------------------------------------
//  <copyright file="ClusterMemberTableClientSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Discovery.Azure.Model;
using Akka.Discovery.Azure.Tests.Utils;
using Akka.Event;
using Azure.Data.Tables;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Azure.Tests
{
    public class ClusterMemberTableClientSpec: TestKit.Xunit2.TestKit, IAsyncLifetime
    {
        private const string ConnectionString = "UseDevelopmentStorage=true";
        private const string ServiceName = nameof(ServiceName);
        private const string WrongService = nameof(WrongService);
        private const string TableName = "AkkaDiscoveryClusterMembers";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int FirstPort = 12345;

        private readonly ClusterMemberTableClient _client;
        private readonly TableClient _rawClient;

        private int _lastPort = FirstPort;

        public ClusterMemberTableClientSpec(ITestOutputHelper helper)
            : base("akka.loglevel = DEBUG", nameof(ClusterMemberTableClientSpec), helper)
        {
            var logger = Logging.GetLogger(Sys, nameof(ClusterMemberTableClient));
            _client = new ClusterMemberTableClient(ServiceName, ConnectionString, TableName, logger);
            _rawClient = new TableClient(ConnectionString, TableName);
        }
        
        public async Task InitializeAsync()
        {
            // Tables are wiped out at every test start
            await DbUtils.Cleanup(ConnectionString);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact(DisplayName = "GetOrCreateAsync should insert a new entry")]
        public async Task GetOrCreateInsert()
        {
            // Test will fail here if the client did not create the appropriate table
            var entity = await _client.GetOrCreateAsync(_address, FirstPort);
            
            // There should be 1 entry inside the table
            var entries = new List<TableEntity>();
            await foreach(var entry in _rawClient.QueryAsync<TableEntity>())
            {
                entries.Add(entry);
            }
            entries.Count.Should().Be(1);

            var tableEntity = ClusterMember.FromEntity(entries[0]);
            entity.Should().Be(tableEntity);
        }

        [Fact(DisplayName = "GetOrCreateAsync should fetch existing entry WITHOUT updating LastUpdate")]
        public async Task GetOrCreateFetch()
        {
            await PopulateTable();
            
            // The "proper" entry is populated as if it was updated 4 hours ago
            var entity = await _client.GetOrCreateAsync(_address, FirstPort);
            var now = DateTime.UtcNow;
            entity.LastUpdate.Should().BeApproximately(now - 4.Hours(), 1.Seconds());
        }

        
        [Fact(DisplayName = "GetAllAsync should filter entries on LastUpdate")]
        public async Task GetAllFilters()
        {
            await PopulateTable();
            
            // initialize internal cache
            await _client.GetOrCreateAsync(_address, FirstPort);
            
            var lastUpdate = DateTime.UtcNow - 20.Seconds();
            // Grab all entries from the correct service
            var entries = await _client.GetAllAsync(lastUpdate.Ticks);
            
            entries.Count.Should().Be(3);
            foreach (var entry in entries)
            {
                entry.ServiceName.Should().Be(ServiceName);
                entry.LastUpdate.Should().BeAfter(lastUpdate);
            }
        }

        [Fact(DisplayName = "UpdateAsync should update LastUpdate to now")]
        public async Task UpdateUpdatesLastUpdate()
        {
            await PopulateTable();
            
            // populate the internal cache
            await _client.GetOrCreateAsync(_address, FirstPort);

            // update should also update the table entry
            (await _client.UpdateAsync()).Should().BeTrue();

            // Retrieve the entry directly from the table and check LastUpdate value
            var entry = await _client.GetEntityAsync(ClusterMember.CreateRowKey(_address, FirstPort), default);
            entry.LastUpdate.Should().BeApproximately(DateTime.UtcNow, 500.Milliseconds());
        }

        [Fact(DisplayName = "PruneAsync should prunes entries and only on proper service name")]
        public async Task PruneShouldPruneEntries()
        {
            await PopulateTable();
            
            // populate the internal cache
            await _client.GetOrCreateAsync(_address, FirstPort);

            var lastUpdate = DateTime.UtcNow - 10.Minutes();
            (await _client.PruneAsync(lastUpdate.Ticks)).Should().BeTrue();

            // Grab all entries via the raw client
            var entries = new List<TableEntity>();
            await foreach(var entry in _rawClient.QueryAsync<TableEntity>())
            {
                entries.Add(entry);
            }
            
            // entries should contain 9 items, 3 valid entries and 6 entries from other service
            entries.Count.Should().Be(9);
            entries.Count(e => e.PartitionKey == ServiceName).Should().Be(3);
            entries.Count(e => e.PartitionKey != ServiceName).Should().Be(6);
            
            // entries with correct service name should have its LastUpdate correctly pruned
            foreach (var entry in entries.Where(e => e.PartitionKey == ServiceName))
            {
                var entity = ClusterMember.FromEntity(entry);
                entity.LastUpdate.Should().BeAfter(lastUpdate);
            }
        }
        
        private async Task PopulateTable()
        {
            var batch = new List<TableTransactionAction>();

            var now = DateTime.UtcNow;
            
            // add 3 entries in the past
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(ServiceName, now - 4.Hours())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(ServiceName, now - 3.Hours())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(ServiceName, now - 2.Hours())));
            
            // add 3 valid entries 
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(ServiceName, now - 5.Seconds())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(ServiceName, now - 3.Seconds())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(ServiceName, now)));
            
            // add 3 entries from different service name in the past
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(WrongService, now - 4.Hours())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(WrongService, now - 3.Hours())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(WrongService, now - 2.Hours())));
            
            // add 3 valid entries from different service name
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(WrongService, now - 5.Seconds())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(WrongService, now - 3.Seconds())));
            batch.Add(new TableTransactionAction(TableTransactionActionType.Add, CreateEntity(WrongService, now)));

            await _rawClient.CreateIfNotExistsAsync();
            await _rawClient.SubmitTransactionAsync(batch);
        }

        private TableEntity CreateEntity(string serviceName, DateTime lastUpdate)
        {
            var entry = ClusterMember.CreateEntity(serviceName, _address, _lastPort++);
            entry[ClusterMember.LastUpdateName] = lastUpdate.Ticks;
            return entry;
        }
    }
}