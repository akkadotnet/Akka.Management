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
using Xunit;

namespace Akka.Discovery.Azure.Tests
{
    [Collection(nameof(AzuriteSpecs))]
    public class ClusterMemberTableClientSpec: TestKit.Xunit.TestKit, IAsyncLifetime
    {
        private readonly string ConnectionString;
        private const string ServiceName = nameof(ServiceName);
        private const string WrongService = nameof(WrongService);
        private const string TableName = "AkkaDiscoveryClusterMembers";
        private const string Host = "fake.com";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int FirstPort = 12345;

        private readonly ClusterMemberTableClient _client;
        private readonly TableClient _rawClient;
        private readonly AzuriteFixture _azuriteFixture;

        private int _lastPort = FirstPort;

        public ClusterMemberTableClientSpec(ITestOutputHelper helper, AzuriteFixture azuriteFixture)
            : base("akka.loglevel = DEBUG", nameof(ClusterMemberTableClientSpec), helper)
        {
            _azuriteFixture = azuriteFixture;
            ConnectionString = azuriteFixture.ConnectionString;
            var logger = Logging.GetLogger(Sys, nameof(ClusterMemberTableClient));
            var settings = AzureDiscoverySettings.Empty
                .WithServiceName(ServiceName)
                .WithConnectionString(ConnectionString)
                .WithTableName(TableName);
            _client = new ClusterMemberTableClient(settings, logger);
            _rawClient = new TableClient(ConnectionString, TableName);
        }
        
        public async ValueTask InitializeAsync()
        {
            // Tables are wiped out at every test start
            await DbUtils.Cleanup(ConnectionString);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        [Fact(DisplayName = "GetOrCreateAsync should insert a new entry")]
        public async Task GetOrCreateInsert()
        {
            // Test will fail here if the client did not create the appropriate table
            var entity = await _client.GetOrCreateAsync(Host, _address, FirstPort);
            
            // There should be 1 entry inside the table
            var entries = new List<TableEntity>();
            await foreach(var entry in _rawClient.QueryAsync<TableEntity>())
            {
                entries.Add(entry);
            }
            Assert.Single(entries);

            var tableEntity = ClusterMember.FromEntity(entries[0]);
            Assert.Equal(tableEntity, entity);
        }

        [Fact(DisplayName = "GetOrCreateAsync should fetch existing entry and updates LastUpdate")]
        public async Task GetOrCreateFetch()
        {
            await PopulateTable();
            
            // The entry is populated as if it was updated 4 hours ago
            // GetOrCreateAsync SHOULD update this value during fetch.
            var entity = await _client.GetOrCreateAsync(Host, _address, FirstPort);
            var now = DateTime.UtcNow;
            entity.LastUpdate.BeApproximately(now, TimeSpan.FromSeconds(1));
        }

        
        [Fact(DisplayName = "GetAllAsync should filter entries on LastUpdate")]
        public async Task GetAllFilters()
        {
            await PopulateTable();
            
            // initialize internal cache, this also updates the entry
            await _client.GetOrCreateAsync(Host, _address, FirstPort);
            
            var lastUpdate = DateTime.UtcNow - TimeSpan.FromSeconds(20);
            // Grab all entries from the correct service
            var entries = await _client.GetAllAsync(lastUpdate.Ticks);
            
            Assert.Equal(4, entries.Count);
            foreach (var entry in entries)
            {
                Assert.Equal(ServiceName, entry.ServiceName);
                Assert.True(entry.LastUpdate > lastUpdate);
            }
        }

        [Fact(DisplayName = "UpdateAsync should update LastUpdate to now")]
        public async Task UpdateUpdatesLastUpdate()
        {
            await PopulateTable();
            
            // populate the internal cache
            await _client.GetOrCreateAsync(Host, _address, FirstPort);

            // update should also update the table entry
            var updateException = await Record.ExceptionAsync(async () => await _client.UpdateAsync());
            Assert.Null(updateException);

            // Retrieve the entry directly from the table and check LastUpdate value
            var entry = await _client.GetEntityAsync(ClusterMember.CreateRowKey(Host, _address, FirstPort), default);
            Assert.NotNull(entry);
            entry!.LastUpdate.BeApproximately(DateTime.UtcNow, TimeSpan.FromMilliseconds(500));
        }

        [Fact(DisplayName = "PruneAsync should prunes entries and only on proper service name")]
        public async Task PruneShouldPruneEntries()
        {
            await PopulateTable();
            
            // populate the internal cache, this also updates the entry
            await _client.GetOrCreateAsync(Host, _address, FirstPort);

            var lastUpdate = DateTime.UtcNow - TimeSpan.FromMinutes(10);
            var pruneException = await Record.ExceptionAsync(async () => await _client.PruneAsync(lastUpdate.Ticks));
            Assert.Null(pruneException);

            // Grab all entries via the raw client
            var entries = new List<TableEntity>();
            await foreach(var entry in _rawClient.QueryAsync<TableEntity>())
            {
                entries.Add(entry);
            }
            
            // entries should contain 10 items, 4 valid entries and 6 entries from other service
            Assert.Equal(10, entries.Count);
            Assert.Equal(4, entries.Count(e => e.PartitionKey == ServiceName));
            Assert.Equal(6, entries.Count(e => e.PartitionKey != ServiceName));
            
            // entries with correct service name should have its LastUpdate correctly pruned
            foreach (var entry in entries.Where(e => e.PartitionKey == ServiceName))
            {
                var entity = ClusterMember.FromEntity(entry);
                Assert.True(entity.LastUpdate > lastUpdate);
            }
        }
        
        private async Task PopulateTable()
        {
            var batch = new List<TableTransactionAction>();
            var now = DateTime.UtcNow;
            var add = TableTransactionActionType.Add;
            
            // add 3 entries in the past
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - TimeSpan.FromHours(4)))); // This is the test actual entry
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - TimeSpan.FromHours(3))));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - TimeSpan.FromHours(2))));
            
            // add 3 valid entries 
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - TimeSpan.FromSeconds(5))));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - TimeSpan.FromSeconds(3))));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now)));
            
            // add 3 entries from different service name in the past
            batch.Add(new TableTransactionAction(add, CreateEntity(WrongService, now - TimeSpan.FromHours(4))));
            batch.Add(new TableTransactionAction(add, CreateEntity(WrongService, now - TimeSpan.FromHours(3))));
            batch.Add(new TableTransactionAction(add, CreateEntity(WrongService, now - TimeSpan.FromHours(2))));
            
            // add 3 valid entries from different service name
            batch.Add(new TableTransactionAction(add, CreateEntity(WrongService, now - TimeSpan.FromSeconds(5))));
            batch.Add(new TableTransactionAction(add, CreateEntity(WrongService, now - TimeSpan.FromSeconds(3))));
            batch.Add(new TableTransactionAction(add, CreateEntity(WrongService, now)));

            await _rawClient.CreateIfNotExistsAsync();
            await _rawClient.SubmitTransactionAsync(batch);
        }

        private TableEntity CreateEntity(string serviceName, DateTime lastUpdate)
        {
            var entry = ClusterMember.CreateEntity(serviceName, Host, _address, _lastPort++);
            entry[ClusterMember.LastUpdateName] = lastUpdate.Ticks;
            return entry;
        }
    }
}