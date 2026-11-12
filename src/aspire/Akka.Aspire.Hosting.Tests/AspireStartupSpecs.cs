// -----------------------------------------------------------------------
//  <copyright file="AspireStartupSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;

namespace Akka.Aspire.Hosting.Tests;

public sealed class AspireStartupSpecs
{
    [Fact]
    public async Task Startup_failure_is_propagated_after_cleanup()
    {
        var failure = new InvalidOperationException("startup failed");
        var cleanedUp = false;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AspireClusterFormationSpecs.RunStartupAsync(
                () => Task.FromException(failure),
                () => { cleanedUp = true; return ValueTask.CompletedTask; }));

        Assert.Same(failure, actual);
        Assert.True(cleanedUp);
    }

    [Fact]
    public async Task Startup_and_cleanup_failures_are_both_reported()
    {
        var startupFailure = new InvalidOperationException("startup failed");
        var cleanupFailure = new InvalidOperationException("cleanup failed");

        var actual = await Assert.ThrowsAsync<AggregateException>(() =>
            AspireClusterFormationSpecs.RunStartupAsync(
                () => Task.FromException(startupFailure),
                () => ValueTask.FromException(cleanupFailure)));

        Assert.Collection(actual.InnerExceptions,
            e => Assert.Same(startupFailure, e),
            e => Assert.Same(cleanupFailure, e));
    }

    [Fact]
    public async Task Successful_startup_keeps_the_application_running()
    {
        var cleanedUp = false;
        await AspireClusterFormationSpecs.RunStartupAsync(
            () => Task.CompletedTask,
            () => { cleanedUp = true; return ValueTask.CompletedTask; });
        Assert.False(cleanedUp);
    }
}
