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
        var _ = _lease.Release().ContinueWith(task =>
        {
            probe.Tell(task);
        });

        var task = probe.ExpectMsg<Task<bool>>();
        task.IsFaulted.Should().BeFalse();
        task.Exception.Should().BeNull();
        task.Result.Should().BeTrue();
    }

    [Fact(DisplayName = "Acquire should be idempotent and returns the same task while acquire is in progress")]
    public async Task MultipleAcquireTest()
    {
        var task1 = _lease.Acquire();
        var task2 = _lease.Acquire();
        var task3 = _lease.Acquire();

        task1.Should().Be(task2).And.Be(task3);
        (await task1).Should().BeTrue();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Util.Cleanup("UseDevelopmentStorage=true");
    }
}