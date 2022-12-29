﻿using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Cluster;
using Akka.Cluster.Hosting;
using Akka.Cluster.Hosting.SBR;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.Coordination.KubernetesApi;
using Akka.Discovery.KubernetesApi;
using Akka.Hosting;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using Akka.Util;
using KubernetesCluster.Actors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace KubernetesCluster
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    
                    var systemName = Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim() ?? "ClusterSystem";
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
                                sbrOptions: new LeaseMajorityOption { LeaseImplementation = KubernetesLeaseOption.Instance });
                        
                        // Add Akka.Management.Cluster.Bootstrap support
                        builder.WithClusterBootstrap(setup =>
                            {
                                setup.ContactPointDiscovery.ServiceName = "clusterbootstrap";
                                setup.ContactPointDiscovery.PortName = "management";
                            }, autoStart: true);
                        
                        // Add Akka.Discovery.KubernetesApi support
                        builder.WithKubernetesDiscovery(setup =>
                        {
                            setup.PodLabelSelector = "app=clusterbootstrap";
                            setup.RawIp = false;
                        });
                        
                        // Add Akka.Coordination.KubernetesApi support
                        builder.WithKubernetesLease();
                        
                        // Add https://cmd.petabridge.com/ for diagnostics
                        builder.AddHocon(@"
                            petabridge.cmd {
	                            # default IP address used to listen for incoming petabridge.cmd client connections
	                            # should be a safe default as it listens on all network interfaces.
                                host = ""0.0.0.0""

                                # default port number used to listen for incoming petabridge.cmd client connections
                                port = 9110
                            }", HoconAddMode.Prepend)
                            .AddPetabridgeCmd(pbm =>
                            {
                                pbm.RegisterCommandPalette(ClusterCommands.Instance);
                                pbm.RegisterCommandPalette(new RemoteCommands());
                            });

                        // Add start-up code
                        builder.AddStartup((system, registry) =>
                        {
                            var cluster = Cluster.Get(system);
                            cluster.RegisterOnMemberUp(() =>
                            {
                                var chaos = system.ActorOf(ChaosActor.Props(), "chaos");
                                var subscriber = system.ActorOf(SubscriberActor.Props(), "subscriber");
                                var listener = system.ActorOf(ClusterListener.Props(), "listener");
                                
                                var mediator = DistributedPubSub.Get(system).Mediator;
                                system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () =>
                                {
                                    mediator.Tell(new Publish("content", ThreadLocalRandom.Current.Next(0, 10)));
                                    //chaos.Tell(ThreadLocalRandom.Current.Next(0,200));
                                });
                            });
                        });
                    });
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddConsole();
                })
                .Build();

            await host.RunAsync();
        }
    }
}