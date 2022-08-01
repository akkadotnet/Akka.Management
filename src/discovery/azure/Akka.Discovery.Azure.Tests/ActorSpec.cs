// -----------------------------------------------------------------------
//  <copyright file="ActorSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Discovery.Azure.Actors;
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
    public class ActorSpec: TestKit.Xunit2.TestKit, IAsyncLifetime
    {
        private static readonly Configuration.Config Config = ConfigurationFactory.ParseString(@"
akka.loglevel = DEBUG
akka.actor.provider = cluster
akka.remote.dot-netty.tcp.port = 0
");
        
        private const string ConnectionString = "UseDevelopmentStorage=true";
        private const string ServiceName = nameof(ServiceName);
        private const string TableName = "AkkaDiscoveryClusterMembers";
        private const string Host = "fake.com";
        private readonly IPAddress _address = IPAddress.Loopback;
        private const int FirstPort = 12345;

        private readonly ClusterMemberTableClient _client;
        private readonly TableClient _rawClient;

        private int _lastPort = FirstPort;
        
        public ActorSpec(ITestOutputHelper helper)
            : base(Config, nameof(ClusterMemberTableClientSpec), helper)
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

        [Fact(DisplayName = "HeartbeatActor should update ClusterMember entry")]
        public async Task HeartbeatActorShouldUpdate()
        {
            var settings = AzureDiscoverySettings.Empty
                .WithConnectionString(ConnectionString)
                .WithServiceName(ServiceName)
                .WithTableName(TableName);

            // Initialize client
            var firstEntry = await _client.GetOrCreateAsync(Host, _address, FirstPort);
            var actor = Sys.ActorOf(HeartbeatActor.Props(settings, _client));

            await WithinAsync(3.Seconds(), async () =>
            {
                await EventFilter.Debug(contains: "LastUpdate successfully updated from")
                    .ExpectOneAsync(async () =>
                    {
                        actor.Tell("heartbeat", actor); // Fake a timer message
                    });
            });

            var members = await GetEntriesAsync();
            members.Count.Should().Be(1);

            members[0].LastUpdate.Should().BeAfter(firstEntry.LastUpdate);
        }

        [Fact(DisplayName = "PruneActor should prune ClusterMember entries")]
        public async Task PruneActorShouldPrune()
        {
            var cluster = Cluster.Cluster.Get(Sys);
            var selfAddress = cluster.SelfAddress;
            
            var settings = AzureDiscoverySettings.Empty
                .WithConnectionString(ConnectionString)
                .WithServiceName(ServiceName)
                .WithTableName(TableName);

            await PopulateTable();

            var members = await GetEntriesAsync();
            members.Count.Should().Be(9);
            
            // Initialize client
            await _client.GetOrCreateAsync(Host, _address, FirstPort);
            var actor = Sys.ActorOf(PruneActor.Props(settings, _client));

            // Simulate leadership acquisition 
            actor.Tell(new ClusterEvent.LeaderChanged(selfAddress), Nobody.Instance);
            
            await WithinAsync(3.Seconds(), async () =>
            {
                await EventFilter.Debug(contains: "row entries pruned:")
                    .ExpectOneAsync(async () =>
                    {
                        actor.Tell("prune", actor); // Fake a timer message
                    });
            });

            members = await GetEntriesAsync();
            members.Count.Should().Be(4);
        }

        private async Task<List<ClusterMember>> GetEntriesAsync()
        {
            var members = new List<ClusterMember>();
            var query = _rawClient.QueryAsync<TableEntity>();
            await foreach (var entry in query)
            {
                members.Add(ClusterMember.FromEntity(entry));
            }

            return members;
        }

        private async Task PopulateTable()
        {
            var batch = new List<TableTransactionAction>();
            var now = DateTime.UtcNow;
            var add = TableTransactionActionType.Add;
            
            // add 6 entries in the past
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 9.Hours())));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 8.Hours())));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 7.Hours())));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 6.Hours())));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 5.Hours())));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 4.Hours())));
            
            // add 3 valid entries 
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 5.Seconds())));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now - 3.Seconds())));
            batch.Add(new TableTransactionAction(add, CreateEntity(ServiceName, now)));
            
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