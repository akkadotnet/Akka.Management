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
using Akka.Util;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Coordination.Azure.Tests
{
    public class AzureApiSpec : TestKit.Xunit2.TestKit, IAsyncLifetime
    {
        private readonly AzureLeaseSettings _settings;
        private readonly AzureApiImpl _underTest;
        private const string LeaseName = "lease-1";
        
        private static readonly Config BaseConfig = 
            ConfigurationFactory.ParseString(@"
                akka.loglevel=DEBUG
                akka.remote.dot-netty.tcp.port = 0");
        
        public AzureApiSpec(ITestOutputHelper output) : base(BaseConfig, nameof(AzureApiSpec), output)
        {
            _settings = AzureLeaseSettings.Empty
                .WithConnectionString("UseDevelopmentStorage=true")
                .WithApiServiceRequestTimeout(800.Milliseconds());
                
            _underTest = new AzureApiImpl(Sys, _settings);
        }
        
        public async Task InitializeAsync()
        {
            await Util.Cleanup("UseDevelopmentStorage=true");
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
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
    }
}