//-----------------------------------------------------------------------
// <copyright file="AzureApiSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

#nullable enable
using System;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Coordination.Azure.Internal;
using Akka.Discovery.Azure.Tests;
using Akka.Util;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Akka.Coordination.Azure.Tests
{
    [Collection(nameof(AzuriteSpecs))]
    public class AzureApiSpec : TestKit.Xunit.TestKit, IAsyncLifetime
    {
        private readonly AzureLeaseSettings _settings;
        private readonly AzureApiImpl _underTest;
        private const string LeaseName = "lease-1";
        private readonly AzuriteFixture _fixture;
        private readonly string _connectionString;
        
        private static readonly Config BaseConfig = 
            ConfigurationFactory.ParseString(@"
                akka.loglevel=DEBUG
                akka.remote.dot-netty.tcp.port = 0");
        
        public AzureApiSpec(ITestOutputHelper output, AzuriteFixture fixture) : base(BaseConfig, nameof(AzureApiSpec), output)
        {
            _fixture = fixture;
            _connectionString = _fixture.ConnectionString;
            _settings = AzureLeaseSettings.Empty
                .WithConnectionString(_connectionString)
                .WithApiServiceRequestTimeout(800.Milliseconds());
                
            _underTest = new AzureApiImpl(Sys, _settings);
        }
        
        public async ValueTask InitializeAsync()
        {
            await Util.Cleanup(_connectionString);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        [Fact(DisplayName = "Azure lease resource should be able to be created")]
        public async Task AbleToCreateLeaseResource()
        {
            (await _underTest.RemoveLease(LeaseName)).Should().Be(Done.Instance);
            var leaseRecord = await _underTest.ReadOrCreateLeaseResource(LeaseName);
            leaseRecord.Owner.Should().BeNull();
            leaseRecord.Version.Should().NotBeNull();
        }

        [Fact(DisplayName = "Azure lease resource should update a lease successfully")]
        public async Task AbleToUpdateLease()
        {
            const string owner = "client1";

            var created = await _underTest.ReadOrCreateLeaseResource(LeaseName);
            
            var response = await _underTest.UpdateLeaseResource(LeaseName, owner, created.Version, DateTimeOffset.UtcNow);
            response.Should().BeOfType<Right<LeaseResource, LeaseResource>>();
            var right = ((Right<LeaseResource, LeaseResource>)response).Value;
            right.Owner.Should().Be(owner);
            right.Version.Should().NotBe(created.Version);
            right.Time.Should().BeAfter(created.Time);
        }

        [Fact(DisplayName = "Azure lease resource should update a lease conflict")]
        public async Task ShouldUpdateLeaseConflict()
        {
            const string owner = "client1";
            const string conflictOwner = "client2";
            
            var created = await _underTest.ReadOrCreateLeaseResource(LeaseName);
            var updateResponse = await _underTest.UpdateLeaseResource(LeaseName, conflictOwner, created.Version, DateTimeOffset.UtcNow);
            var updated = ((Right<LeaseResource, LeaseResource>)updateResponse).Value;

            var response = await _underTest.UpdateLeaseResource(LeaseName, owner, created.Version, DateTimeOffset.UtcNow);
            response.Should().BeOfType<Left<LeaseResource, LeaseResource>>();
            var left = ((Left<LeaseResource, LeaseResource>)response).Value;
            left.Owner.Should().Be(conflictOwner);
            left.Version.Should().Be(updated.Version);
            left.Time.Should().Be(updated.Time);

        }

        [Fact(DisplayName = "Azure lease resource should remove lease")]
        public async Task ShouldRemoveLease()
        {
            var created = await _underTest.ReadOrCreateLeaseResource(LeaseName);

            var response = await _underTest.RemoveLease(LeaseName);
            response.Should().Be(Done.Instance);
        }

        // Regression test for https://github.com/akkadotnet/Akka.Management/issues/3397
        // A second AzureApiImpl instance (_initialized = false) encountering a container that
        // already exists must handle the 409 ContainerAlreadyExists gracefully instead of
        // throwing a LeaseException that propagates up to the Split Brain Resolver.
        [Fact(DisplayName = "Should handle ContainerAlreadyExists when a second instance starts")]
        public async Task ShouldHandleContainerAlreadyExists()
        {
            // First instance creates the container and lease blob
            var firstLease = await _underTest.ReadOrCreateLeaseResource(LeaseName);
            firstLease.Owner.Should().BeNull();

            // Second instance has _initialized = false, so ContainerClient() will call
            // CreateAsync() and receive a 409 ContainerAlreadyExists from Azure.
            // Before the fix, this threw LeaseException and crashed the lease actor.
            var secondInstance = new AzureApiImpl(Sys, _settings);
            var secondLease = await secondInstance.ReadOrCreateLeaseResource(LeaseName);
            secondLease.Owner.Should().BeNull();
            secondLease.Version.Should().NotBeNull();
        }

        // Verifies that multiple independent AzureApiImpl instances can operate concurrently
        // against the same container — the typical scenario in a multi-node Akka.NET cluster
        // where each node creates its own AzureApiImpl.
        [Fact(DisplayName = "Multiple instances should acquire different leases against same container")]
        public async Task MultipleInstancesShouldAcquireDifferentLeases()
        {
            const string leaseName1 = "lease-multi-1";
            const string leaseName2 = "lease-multi-2";
            const string owner1 = "node-1";
            const string owner2 = "node-2";

            var instance1 = new AzureApiImpl(Sys, _settings);
            var instance2 = new AzureApiImpl(Sys, _settings);

            // Both instances create their leases (both will try CreateAsync on the container)
            var lease1 = await instance1.ReadOrCreateLeaseResource(leaseName1);
            var lease2 = await instance2.ReadOrCreateLeaseResource(leaseName2);

            // Both should succeed — one creates the container, the other gets 409 and handles it
            lease1.Owner.Should().BeNull();
            lease2.Owner.Should().BeNull();

            // Both instances should be able to update their respective leases
            var update1 = await instance1.UpdateLeaseResource(leaseName1, owner1, lease1.Version, DateTimeOffset.UtcNow);
            update1.Should().BeOfType<Right<LeaseResource, LeaseResource>>();

            var update2 = await instance2.UpdateLeaseResource(leaseName2, owner2, lease2.Version, DateTimeOffset.UtcNow);
            update2.Should().BeOfType<Right<LeaseResource, LeaseResource>>();
        }
    }
}