using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.Cluster.Hosting;
using Akka.Cluster.Tools.Client;
using Akka.Hosting;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Discovery.Azure.Tests;

[Collection(nameof(AzuriteSpecs))]
public class MultiDiscoveryIntegrationSpec: Hosting.TestKit.TestKit
{
    private const string ClusterServiceName = "cluster-service";
    private const string ClientServiceName = "client-service";
    private const string ClientConfigPath = "azure-cluster-client";
    private const string ClientTableName = "akkaclusterreceptionists";
    
    internal class ClusterClientKey;

    internal class EchoActorKey;

    private readonly ITestOutputHelper _output;
    private readonly string _connectionString;
    private IHost? _host2;
    private ActorSystem? _sys2;
    
    public MultiDiscoveryIntegrationSpec(ITestOutputHelper output, AzuriteFixture azuriteFixture) : base(nameof(MultiDiscoveryIntegrationSpec), output, logLevel: LogLevel.Debug)
    {
        _output = output;
        _connectionString = azuriteFixture.ConnectionString;
    }

    protected override async Task BeforeTestStart()
    {
        var hostBuilder = new HostBuilder();
        if (Output != null)
            hostBuilder.ConfigureLogging(logger =>
                {
                    logger.ClearProviders();
                    logger.AddProvider(new XUnitLoggerProvider(Output, LogLevel));
                    logger.AddFilter("Akka.*", LogLevel);
                });

        hostBuilder.ConfigureServices(services => 
        {
            services.AddAkka(ActorSystemName, builder =>
            {
                builder
                    .ConfigureLoggers(logger =>
                    {
                        logger.LogLevel = ToAkkaLogLevel(LogLevel);
                        logger.ClearLoggers();
                        logger.AddLoggerFactory();
                    })
                    .WithRemoting(hostname: "localhost", port: 12553)
                    .WithClustering(new ClusterOptions
                    {
                        MinimumNumberOfMembers = 2
                    })
                    .WithAkkaManagement(config =>
                    {
                        config.Http.HostName = "localhost";
                        config.Http.Port = 18559;
                        config.Http.BindHostName = "localhost";
                        config.Http.BindPort = 18559;
                    })
                    .WithClusterBootstrap(setup =>
                    {
                        setup.ContactPointDiscovery.ServiceName = ClusterServiceName;
                        setup.ContactPointDiscovery.RequiredContactPointsNr = 2;
                        setup.ContactPoint.FilterOnFallbackPort = false;
                    })
                    .WithAzureDiscovery(opt =>
                    {
                        opt.ConnectionString = _connectionString;
                        opt.ServiceName = ClusterServiceName;
                        opt.HostName = "localhost";
                        opt.Port = 18559;
                    })
                    .WithAzureDiscovery(opt =>
                    {
                        opt.IsDefaultPlugin = false;
                        opt.ConfigPath = ClientConfigPath;
                        opt.ServiceName = ClientServiceName;
                        opt.TableName = ClientTableName;
                        opt.ConnectionString = _connectionString;
                        opt.HostName = "localhost";
                        opt.Port = 18559;
                    })
                    .WithClusterClientReceptionist()
                    .WithActors((system, registry) =>
                    {
                        var echoActor = system.ActorOf(dsl =>
                        {
                            dsl.ReceiveAny((msg, ctx) => ctx.Sender.Tell(msg));
                        }, "echo");
                        ClusterClientReceptionist.Get(system).RegisterService(echoActor);
                        registry.Register<EchoActorKey>(echoActor);
                    })
                    .AddStartup((system, registry) =>
                    {
                        Discovery.Get(system).LoadServiceDiscovery(ClientConfigPath);
                    });
                
            });
        });

        _host2 = hostBuilder.Build();
        await _host2.StartAsync();
        _sys2 = _host2.Services.GetRequiredService<ActorSystem>();
    }

    private static Event.LogLevel ToAkkaLogLevel(LogLevel logLevel)
        => logLevel switch
        {
            LogLevel.Trace => Event.LogLevel.DebugLevel,
            LogLevel.Debug => Event.LogLevel.DebugLevel,
            LogLevel.Information => Event.LogLevel.InfoLevel,
            LogLevel.Warning => Event.LogLevel.WarningLevel,
            LogLevel.Error => Event.LogLevel.ErrorLevel,
            LogLevel.Critical => Event.LogLevel.ErrorLevel,
            _ => Event.LogLevel.ErrorLevel
        };
    
    protected override async Task AfterAllAsync()
    {
        if (_host2 is null)
            return;
        
        await _host2.StopAsync();
        _host2.Dispose();
        _host2 = null;
        _sys2 = null;
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder
            .WithRemoting(hostname: "localhost", port: 12552)
            .WithClustering(new ClusterOptions
            {
                MinimumNumberOfMembers = 2
            })
            .WithAkkaManagement(config =>
            {
                config.Http.HostName = "localhost";
                config.Http.Port = 18558;
                config.Http.BindHostName = "localhost";
                config.Http.BindPort = 18558;
            })
            .WithClusterBootstrap(setup =>
            {
                setup.ContactPointDiscovery.ServiceName = ClusterServiceName;
                setup.ContactPointDiscovery.RequiredContactPointsNr = 2;
                setup.ContactPoint.FilterOnFallbackPort = false;
            })
            .WithAzureDiscovery(opt =>
            {
                opt.ConnectionString = _connectionString;
                opt.ServiceName = ClusterServiceName;
                opt.HostName = "localhost";
                opt.Port = 18558;
            })
            .WithClusterClientDiscovery<ClusterClientKey>(opt =>
            {
                opt.DiscoveryOptions = new AkkaDiscoveryOptions
                {
                    IsDefaultPlugin = false,
                    ReadOnly = true,
                    ConfigPath = ClientConfigPath,
                    ConnectionString = _connectionString,
                    ServiceName = ClientServiceName,
                    TableName = ClientTableName,
                    HostName = "localhost",
                    Port = 18558,
                };
                opt.ServiceName = ClientServiceName;
                opt.ClientActorName = "test-actor-client";
                opt.RetryInterval = TimeSpan.FromSeconds(0.2);
            })
            .AddHocon(
                """
                akka.cluster.client {
                    heartbeat-interval = 1s
                    acceptable-heartbeat-pause = 2s
                    reconnect-timeout = 10s
                    verbose-logging = true
                    discovery.probe-timeout = 1s
                }
                """, HoconAddMode.Prepend);
    }

    [Fact(DisplayName = "ActorSystem with multiple Azure discovery plugins running at the same time must work")]
    public async Task MultiDiscoveryTest()
    {
        // Cluster must form
        var tcs = new TaskCompletionSource();
        var cluster = Cluster.Cluster.Get(Sys);
        cluster.RegisterOnMemberUp(() => tcs.SetResult());
        
        // Cluster client must find receptionist
        var client = await ActorRegistry.GetAsync<ClusterClientKey>();
        var clientTask = client.Ask<string>(new ClusterClient.Send("/user/echo", "test"), 20.Seconds());
        
        // Both tasks must complete successfully
        await Task.WhenAll(tcs.Task, clientTask).WaitAsync(20.Seconds());
        
        (await clientTask).Should().Be("test");
    }
    
}