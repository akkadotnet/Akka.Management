using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Hosting.SBR;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Coordination.KubernetesApi;
using Akka.Discovery.KubernetesApi;
using Akka.Event;
using Akka.Hosting;
using Akka.Management.Cluster.Bootstrap;
using Kubernetes.StressTest.Actors;
using Kubernetes.StressTest.Cmd;
using Kubernetes.StressTest.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Kubernetes.StressTest
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddCommandLine(args);
                    builder.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    
                    var systemName = hostContext.Configuration.GetValue<string>("actorsystem")?.Trim() ?? "ClusterSystem"; 
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
                            // Empty hostname is intentional and necessary to make sure that remoting binds to the public IP address
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
                                    LeaseImplementation = new KubernetesLeaseOption()
                                };
                            });
                        
                        // Add Akka.Management.Cluster.Bootstrap support
                        builder.WithClusterBootstrap(setup =>
                            {
                                setup.ContactPointDiscovery.ServiceName = "clusterbootstrap";
                                setup.ContactPointDiscovery.PortName = "management";
                                setup.ContactPointDiscovery.RequiredContactPointsNr = 2;
                                setup.ContactPointDiscovery.StableMargin = TimeSpan.FromSeconds(5);
                                setup.ContactPointDiscovery.ContactWithAllContactPoints = true;
                            }, autoStart: true);
                        
                        // Add Akka.Discovery.KubernetesApi support
                        builder.WithKubernetesDiscovery(setup =>
                        {
                            setup.PodLabelSelector = "app=stress-test";
                            setup.RawIp = false;
                        });
                        
                        // Add Akka.Coordination.KubernetesApi support
                        builder.WithKubernetesLease();
                        
                        // Add https://cmd.petabridge.com/ for diagnostics
                        builder.AddHocon("""
                            petabridge.cmd {
                                # default IP address used to listen for incoming petabridge.cmd client connections
                                # should be a safe default as it listens on all network interfaces.
                                host = "0.0.0.0"

                                # default port number used to listen for incoming petabridge.cmd client connections
                                port = 9110
                            }
                            """, HoconAddMode.Prepend)
                            .AddPetabridgeCmd(pbm =>
                            {
                                pbm.RegisterCommandPalette(ClusterCommands.Instance);
                                pbm.RegisterCommandPalette(new RemoteCommands());
                                pbm.RegisterCommandPalette(new TestCommands());
                            });

                        // Add start-up code
                        builder.AddStartup((system, registry) =>
                        {
                            var chaos = system.ActorOf(Props.Create<ChaosActor>(), "chaos");
                            var subscriber = system.ActorOf(Props.Create(() => new Subscriber()), "subscriber");
                            var leaderDowner = system.ActorOf(Props.Create(() => new LeaderDowningActor()), "leader-downer");
                            var mediator = DistributedPubSub.Get(system).Mediator;
                            
                            system.EventStream.Subscribe(leaderDowner, typeof(Debug)); // Enable leader downer
                            /*
                            system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () =>
                            {
                                //mediator.Tell(new Publish("content", ThreadLocalRandom.Current.Next(0, 10)));
                                chaos.Tell(ThreadLocalRandom.Current.Next(0,200));
                            });
                            */
                        });
                    });
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddConsole();
                })
                .UseConsoleLifetime()
                .Build();
            
            await host.RunAsync();
            await Task.Delay(TimeSpan.FromSeconds(40));
        }
    }
}