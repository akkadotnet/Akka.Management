// -----------------------------------------------------------------------
//  <copyright file="AzureLeaseSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Akka.Coordination.Azure.Tests;

public class AzureLeaseSpec: TestKit.Xunit2.TestKit, IAsyncLifetime
{
    private const string LeaseName = "lease";
    private const string OwnerName = "owner1";
    
    private static Config Config()
        => ConfigurationFactory.ParseString(@"
akka.loglevel=DEBUG
akka.stdout-loglevel=DEBUG
akka.actor.debug.fsm=true
akka.remote.dot-netty.tcp.port = 0
akka.coordination.lease.azure.connection-string = ""UseDevelopmentStorage=true""
")
            .WithFallback(AzureLease.DefaultConfiguration);

    private readonly Lease _lease;

    public AzureLeaseSpec(ITestOutputHelper helper): base(Config(), nameof(AzureLeaseSpec), helper)
    {
        _lease = LeaseProvider.Get(Sys).GetLease(LeaseName, "akka.coordination.lease.azure", OwnerName);
    }

    [Fact(DisplayName = "Releasing non-acquired lease should not throw an exception")]
    public void NonAcquiredReleaseTest()
    {
        var probe = CreateTestProbe();
        var _ = _lease.Release().ContinueWith(r =>
        {
            r.IsFaulted.Should().BeTrue();
            r.Exception.Should().NotBeNull();
            var exception = r.Exception!.InnerException;
            exception.Should().NotBeNull();
            exception.Should().BeOfType<LeaseException>();
            exception!.Message.Should().Be("Tried to release a lease that is not acquired");
            probe.Tell(Done.Instance);
        });

        probe.ExpectMsg<Done>();
    }

    public async Task InitializeAsync()
    {
        await Util.Cleanup("UseDevelopmentStorage=true");
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}