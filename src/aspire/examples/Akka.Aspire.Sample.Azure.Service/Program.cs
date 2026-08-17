// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Aspire;
using Akka.Discovery.Azure;
using Akka.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry - ships logs, traces, and metrics to the Aspire dashboard
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .UseOtlpExporter();

builder.Services.AddAkka("SampleSystem", (akkaBuilder, sp) =>
{
    akkaBuilder.ConfigureLoggers(setup =>
    {
        setup.ClearLoggers();
        setup.AddLoggerFactory();
    });

    akkaBuilder.WithAspireClusterBootstrap(sp,
        configureDiscovery: (b, config) =>
        {
            var azureConn = config.GetConnectionString("akka-discovery");
            if (!string.IsNullOrEmpty(azureConn))
                b.WithAzureDiscovery(azureConn, config["Akka:Cluster:ServiceName"]);
        },
        clusterConfigure: c => c.Roles = ["sample"]);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("liveness") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("readiness") });
app.MapGet("/", () => "Hello from Akka.NET Aspire Azure Sample!");

app.Run();
