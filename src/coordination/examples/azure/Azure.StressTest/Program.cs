// See https://aka.ms/new-console-template for more information

using System.Net;
using Akka.Event;
using Akka.Management;
using Azure.StressTest.Actors;
using Azure.StressTest.Cmd;
using Azure.StressTest.Configuration;
using Microsoft.Extensions.Configuration;

namespace Azure.StressTest;

public static class Program
{
public static async Task Main(params string[] args)
{
using var host = new HostBuilder()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddCommandLine(args);
        builder.AddEnvironmentVariables();
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddLogging();
        
        var systemName = hostContext.Configuration.GetValue<string>("actorsystem")?.Trim() ?? "AkkaService";
        services.AddAkka(systemName, (builder, provider) =>
        {
            // don't shutdown gracefully if SIGTERM is received
            builder.AddHocon(
                """
                akka.coordinated-shutdown.run-by-clr-shutdown-hook = off
                akka.coordinated-shutdown.run-by-actor-system-terminate = off
                """, 
                HoconAddMode.Prepend);

            // Add HOCON configuration from Docker
            builder.BootstrapFromDocker(
                provider,
                // Add Akka.Remote support.
                remoteOptions =>
                {
                    remoteOptions.HostName = "";
                    remoteOptions.Port = 4053;
                },
                // Add Akka.Cluster support
                clusterOptions =>
                {
                    clusterOptions.Roles = new []{ "cluster" };
                    clusterOptions.SplitBrainResolver = new LeaseMajorityOption
                    {
                        LeaseImplementation = new AzureLeaseOption()
                    };
                });
            
            // Add Akka.Coordination.Azure lease support
            builder.WithAzureLease(setup => { setup.ConnectionString = ConnectionString(); });
            
            // Add Akka.Management support
            var configuration = provider.GetRequiredService<IConfiguration>();
            var clusterConfigOptions = configuration.GetSection("cluster").Get<ClusterConfigOptions>();
            builder.WithAkkaManagement(setup =>
            {
                setup.Http.HostName = clusterConfigOptions.Ip;
            });
            
            // Add Akka.Management.Cluster.Bootstrap support
            builder.WithClusterBootstrap(setup =>
            {
                setup.ContactPointDiscovery.ServiceName = "clusterbootstrap";
                setup.ContactPointDiscovery.PortName = "management";
            }, autoStart: true);

            // Add Akka.Discovery.Azure support
            builder.WithAzureDiscovery(
                connectionString: ConnectionString(),
                serviceName: "clusterbootstrap", 
                publicHostname: clusterConfigOptions.Ip);
            
            // Add https://cmd.petabridge.com/ for diagnostics
            builder.WithPetabridgeCmd("0.0.0.0", 9110, ClusterCommands.Instance, new RemoteCommands(), new TestCommands());

            // Add start-up code
            builder.AddTestActors();
        });
    })
    .ConfigureLogging((hostContext, configLogging) =>
    {
        configLogging.AddConsole();
    })
    .UseConsoleLifetime()
    .Build();

await host.RunAsync();
}
    
    private const string AzuriteConnectionString = 
        "DefaultEndpointsProtocol=http;" +
        "AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://{0}:10000/devstoreaccount1;" +
        "QueueEndpoint=http://{0}:10001/devstoreaccount1;" +
        "TableEndpoint=http://{0}:10002/devstoreaccount1;";
    private static string ConnectionString()
    {
        var azuriteHost = Environment.GetEnvironmentVariable("AZURITE_HOST")?.Trim() ?? "localhost";
        return string.Format(AzuriteConnectionString, azuriteHost);
    }

    private static AkkaConfigurationBuilder WithPetabridgeCmd(
        this AkkaConfigurationBuilder builder,
        string? hostname = null,
        int? port = null,
        params CommandPaletteHandler[] palettes) 
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(hostname))
            sb.AppendFormat("host = {0}\n", hostname);
        if(port != null)
            sb.AppendFormat("port = {0}\n", port);

        if (sb.Length > 0)
        {
            sb.Insert(0, "petabridge.cmd {\n");
            sb.Append("}");

            builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
        }
        
        return builder.AddPetabridgeCmd(cmd =>
        {
            foreach (var palette in palettes)
            {
                cmd.RegisterCommandPalette(palette);
            }
        });
    }

    private static void AddTestActors(this AkkaConfigurationBuilder builder)
    {
        builder.AddStartup((system, registry) =>
        {
            var log = Logging.GetLogger(system, "Program.Main");
            var cluster = Cluster.Get(system);
            var useChaos = Environment.GetEnvironmentVariable("USE_CHAOS")?.Trim().ToLowerInvariant();
            var usePubSub = Environment.GetEnvironmentVariable("USE_PUBSUB")?.Trim().ToLowerInvariant();
            var listener = system.ActorOf(ClusterListener.Props(), "listener");
            
            cluster.RegisterOnMemberUp(() =>
            {
                log.Warning(">>>>>>>>>>>> Cluster is UP");
                if (useChaos is "true")
                {
                    var chaos = system.ActorOf(Props.Create<ChaosActor>(), "chaos");
                    Cluster.Get(system).RegisterOnMemberUp(() =>
                    {
                        system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () =>
                        {
                            chaos.Tell(ThreadLocalRandom.Current.Next(0,200));
                        });
                    });
                }
                
                if (usePubSub is "true")
                {
                    var mediator = DistributedPubSub.Get(system).Mediator;
                    var subscriber = system.ActorOf(SubscriberActor.Props(), "subscriber");
                    Cluster.Get(system).RegisterOnMemberUp(() =>
                    {
                        system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () =>
                        {
                            mediator.Tell(new Publish("content", ThreadLocalRandom.Current.Next(0,10)));
                        });
                    });
                }
            });
        });
    }    
}

