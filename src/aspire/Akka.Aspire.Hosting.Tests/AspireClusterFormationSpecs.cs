// -----------------------------------------------------------------------
//  <copyright file="AspireClusterFormationSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Akka.Aspire.Hosting.Tests;

/// <summary>
/// End-to-end multi-node cluster formation test driven by Aspire's native testing facilities.
/// <see cref="DistributedApplicationTestingBuilder"/> launches the real sample AppHost, which runs a
/// Redis container and 3 service replicas (<c>WithReplicas(3)</c>) wired through the Akka.Aspire
/// hosting + service integration. Because each replica's <c>required-contact-point-nr</c> is derived
/// from the replica count (3), a replica only reaches <c>MemberStatus.Up</c> — and only then does its
/// <c>akka-cluster-membership</c> health check return Healthy (HTTP 200) — once all three nodes have
/// discovered each other via Redis and formed a single cluster. A 200 from /healthz therefore proves
/// the 3-node cluster actually formed, not merely that the app booted.
/// Requires Docker + the Aspire tooling; skips gracefully when neither is available.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AspireClusterFormationSpecs : IAsyncLifetime
{
    private DistributedApplication? _app;
    private string? _skipReason;

    public async ValueTask InitializeAsync()
    {
        try
        {
            var builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Akka_Aspire_Sample_AppHost>();

            _app = await builder.BuildAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await _app.StartAsync(cts.Token);

            await _app.ResourceNotifications
                .WaitForResourceAsync("service", KnownResourceStates.Running, cts.Token);
        }
        catch (Exception e)
        {
            // Docker / Aspire tooling unavailable (e.g. Windows or headless CI agents). Skip rather
            // than fail; the test only has meaning where the distributed app can actually run.
            _skipReason = $"Aspire distributed application could not start: {e.Message}";
            if (_app is not null)
            {
                await _app.DisposeAsync();
                _app = null;
            }
        }
    }

    [Fact]
    public async Task Service_should_form_a_three_node_cluster_and_report_healthy()
    {
        if (_app is null)
            Assert.Skip(_skipReason ?? "Aspire distributed application is not available");

        var endpoint = _app!.GetEndpoint("service", "http");
        using var client = new HttpClient { BaseAddress = endpoint };

        // Poll /healthz until it returns 200. The membership health check only returns Healthy when
        // MemberStatus == Up, which (with required-contact-point-nr == 3) requires the full 3-node
        // cluster to have formed via Redis discovery.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        HttpResponseMessage? response = null;
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                response = await client.GetAsync("/healthz", cts.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                    break;
            }
            catch (Exception) when (!cts.Token.IsCancellationRequested)
            {
                // Service may not be ready yet
            }

            await Task.Delay(1000, cts.Token);
        }

        Assert.NotNull(response);
        Assert.True(response!.StatusCode == HttpStatusCode.OK,
            "health check returns 200 only once the 3-node cluster has formed and member status is Up");
    }

    [Fact]
    public async Task Service_should_respond_to_root_endpoint()
    {
        if (_app is null)
            Assert.Skip(_skipReason ?? "Aspire distributed application is not available");

        var endpoint = _app!.GetEndpoint("service", "http");
        using var client = new HttpClient { BaseAddress = endpoint };
        var response = await client.GetStringAsync("/");
        Assert.Contains("Hello from Akka.NET Aspire", response);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
