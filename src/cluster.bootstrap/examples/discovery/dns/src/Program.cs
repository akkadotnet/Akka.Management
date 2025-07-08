using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Configuration;
using Akka.DependencyInjection;
using Akka.Event;
using Akka.Hosting;
using Akka.Cluster.Hosting;
using Akka.Cluster.Hosting.SBR;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Discovery.Dns;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using Akka.Util;
using KubernetesCluster.Actors;
using Petabridge.Cmd;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;
using LogLevel = Akka.Event.LogLevel;

namespace DnsCluster;

public class ClusterConfigOptions
{
    public string? Ip { get; set; }
    public int? Port { get; set; }
    public string[]? Seeds { get; set; }
}
public static class Extensions
{
    public static AkkaConfigurationBuilder BootstrapFromDocker(
        this AkkaConfigurationBuilder builder,
        IServiceProvider provider,
        Action<RemoteOptions>? remoteConfiguration = null,
        Action<ClusterOptions>? clusterConfiguration = null)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var clusterConfigOptions = configuration.GetSection("cluster").Get<ClusterConfigOptions>();

        var remoteOptions = new RemoteOptions
        {
            HostName = "0.0.0.0",
            PublicHostName = clusterConfigOptions.Ip ?? Dns.GetHostName(),
            Port = clusterConfigOptions.Port
        };
        remoteConfiguration?.Invoke(remoteOptions);
        
        var clusterOptions = new ClusterOptions
        {
            SeedNodes = clusterConfigOptions.Seeds
        };
        clusterConfiguration?.Invoke(clusterOptions);

        var akkaConfig = configuration.GetSection("akka");
        if (akkaConfig.GetChildren().Any())
            builder.AddHocon(akkaConfig, HoconAddMode.Prepend);

        builder.WithRemoting(remoteOptions);
        builder.WithClustering(clusterOptions);
        
        var managementPort = configuration.GetValue<int>("management.port", 8558);
        
        builder.AddHocon($"akka.management.http.port = {managementPort}",HoconAddMode.Prepend);
        return builder;
    }
}
public static class Program
{
    // Get container IP address for self-identification
    static string GetContainerIp()
    {
        try
        {
            // Get IP of the container's network interface (typically eth0 in Docker)
            var addresses = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName());
            var ipv4 = addresses.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ipv4?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
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

                // if portName is set resolver uses SRV records, otherwise A/AAAA records
                string? portName = hostContext.Configuration.GetValue<string>("portname")?.Trim();
                var systemName = hostContext.Configuration.GetValue<string>("actorsystem")?.Trim() ?? "ClusterSystem"; 
                var serviceName = hostContext.Configuration.GetValue<string>("servicename")?.Trim() ?? "akkacluster"; 
                var pbmPort = hostContext.Configuration.GetValue<int>("pbm:port", 9110);
                var managementPort = hostContext.Configuration.GetValue<int>("management:port", 8558);
                
                
                services.AddAkka(systemName, (builder, provider) =>
                {
                    builder.ConfigureLoggers(a =>
                    {
                        a.LogLevel = LogLevel.DebugLevel;
                        a.LogConfigOnStart = true;
                    });
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
                            clusterOptions.SplitBrainResolver = new KeepMajorityOption();
                        });
                        
                    // Add Akka.Management.Cluster.Bootstrap support
                    builder.WithClusterBootstrap(setup =>
                    {
                        // When running in Docker, use akkacluster service name
                        // Docker will automatically resolve this to all nodes with this DNS name
                        setup.ContactPointDiscovery.ServiceName = serviceName;
                        setup.ContactPoint.FallbackPort = managementPort; // Use management port (8558), not Akka.Remote port
                        setup.ContactPointDiscovery.PortName = portName;
                    }, autoStart: true);
                    
                    
                    
                    // Configure Akka.Management HTTP endpoint
                    builder.WithAkkaManagement(hostName:  GetContainerIp(), port: managementPort, bindHostname : "0.0.0.0");
                    //     setup.Http.HostName = GetContainerIp(); // Use IP address instead of hostname
                    //     setup.Http.Port = managementPort;
                    //     setup.Port = managementPort;
                    // });
                        
                    // Add Akka.Discovery.Dns support
                    // Configure DNS discovery for Docker environment
                    builder.WithDnsDiscovery();
                    if (portName != null)
                    {
                        // use SRV record resolver if portName was specified 
                        var ns =  hostContext.Configuration.GetValue<string>("nameserver")?.Trim() ?? "127.0.0.1:53";
                        builder.WithAsyncDnsResolver(opt => opt.Nameserver =  ns );
                    }

                    // and set it as the default discovery mechanism
                    builder.WithDnsDiscoveryDefault();
                        
                    // Add https://cmd.petabridge.com/ for diagnostics
                    builder.WithPetabridgeCmd("0.0.0.0", pbmPort, ClusterCommands.Instance, new RemoteCommands());
                        
                    // Add start-up code
                    builder.AddStartup((system, registry) =>
                    {
                        system.Log.Info($"Management port ENV = [{managementPort}]");
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
}