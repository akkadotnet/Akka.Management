// -----------------------------------------------------------------------
//  <copyright file="AzureLeaseSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Azure.Tests;
using Xunit;

namespace Akka.Coordination.Azure.Tests;

[Collection(nameof(AzuriteSpecs))]
public class AzureLeaseSpec: TestKit.Xunit.TestKit, IAsyncLifetime
{
    private const string LeaseName = "lease";
    private const string OwnerName = "owner1";
    
    private static Config Config(string connectionString)
        => ConfigurationFactory.ParseString($"""
                                            akka.loglevel=DEBUG
                                            akka.stdout-loglevel=DEBUG
                                            akka.actor.debug.fsm=true
                                            akka.remote.dot-netty.tcp.port = 0
                                            akka.coordination.lease.azure.connection-string = "{connectionString}"
                                            """)
            .WithFallback(AzureLease.DefaultConfiguration);

    private readonly Lease _lease;
    private readonly AzuriteFixture _fixture;

    public AzureLeaseSpec(ITestOutputHelper helper, AzuriteFixture fixture): base(Config(fixture.ConnectionString), nameof(AzureLeaseSpec), helper)
    {
        _fixture = fixture;
        _lease = LeaseProvider.Get(Sys).GetLease(LeaseName, "akka.coordination.lease.azure", OwnerName);
    }

    [Fact(DisplayName = "Releasing non-acquired lease should not throw an exception")]
    public async Task NonAcquiredReleaseTest()
    {
        var ex = await Record.ExceptionAsync(async () =>
        {
            var result = await _lease.Release();
            Assert.True(result);
        });
        Assert.Null(ex);
    }

    [Fact(DisplayName = "Acquire should be idempotent and returns the same task while acquire is in progress")]
    public async Task MultipleAcquireTest()
    {
        var task1 = _lease.Acquire();
        var task2 = _lease.Acquire();
        var task3 = _lease.Acquire();

        Assert.Equal(task2, task1);
        Assert.Equal(task3, task1);
        Assert.True((await task1));
    }

    public async ValueTask InitializeAsync()
    {
        await Util.Cleanup(_fixture.ConnectionString);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}