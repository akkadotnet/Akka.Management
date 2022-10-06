using System;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Cluster.Hosting.SBR;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.Discovery.Azure;
using Akka.Hosting;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using Akka.Util;
using AzureCluster.Cmd;
using KubernetesCluster.Actors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace AzureCluster
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    
                    var systemName = Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim() ?? "AkkaService";
                    services.AddAkka(systemName, (builder, provider) =>
                    {
                        // Add HOCON configuration from Docker
                        builder.AddHocon(Config.Empty.BootstrapFromDocker(), HoconAddMode.Prepend);
                        
                        // Add Akka.Remote support.
                        // Empty hostname is intentional and necessary to make sure that remoting binds to the public
                        // IP address
                        builder.WithRemoting(hostname: "", port: 4053);
                        
                        // Add Akka.Cluster support
                        builder.WithClustering(
                                options: new ClusterOptions { Roles = new[] { "cluster" } },
                                sbrOptions: new KeepMajorityOption());

                        // Add Akka.Management.Cluster.Bootstrap support
                        builder.WithClusterBootstrap(setup =>
                            {
                                setup.ContactPointDiscovery.ServiceName = "clusterbootstrap";
                                setup.ContactPointDiscovery.PortName = "management";
                            }, autoStart: true);

                        // Add Akka.Discovery.Azure support
                        builder.WithAzureDiscovery(ConnectionString(), serviceName: "clusterbootstrap");
                        
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
            var azuriteHost = Environment.GetEnvironmentVariable("AZURITE_HOST")?.Trim() ?? "azurite";
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
                var cluster = Cluster.Get(system);
                var useChaos = Environment.GetEnvironmentVariable("USE_CHAOS")?.Trim().ToLowerInvariant();
                var usePubSub = Environment.GetEnvironmentVariable("USE_PUBSUB")?.Trim().ToLowerInvariant();
                var listener = system.ActorOf(ClusterListener.Props(), "listener");
                
                cluster.RegisterOnMemberUp(() =>
                {
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
}
