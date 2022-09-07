using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Cluster.Hosting;
using Akka.Configuration;
using Akka.Discovery.AwsApi.Ecs;
using Akka.Hosting;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Akka.Remote.Hosting;
using Akka.Util;
using Microsoft.Extensions.Hosting;

namespace Aws.Ecs
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var privateAddress = GetPrivateAddressOrExit();

            var host = new HostBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddAkka("ecsBootstrapDemo", (builder, provider) =>
                    {
                        builder
                            .AddHocon("akka.discovery.method = aws-api-ecs")
                            .AddHocon(EcsDiscovery.DefaultConfiguration())
                            .WithAkkaManagement(setup =>
                            {
                                setup.Http.Port = 8558;
                                setup.Http.Hostname = privateAddress.ToString();
                            })
                            .WithRemoting(privateAddress.ToString(), 4053)
                            .WithClustering()
                            .WithClusterBootstrap(setup =>
                            {
                                setup.ContactPoint.FallbackPort = 8558;
                            });
                    });
                }).Build();
            
            host.Run();
        }

        private static IPAddress GetPrivateAddressOrExit()
        {
            switch (EcsDiscovery.GetContainerAddress())
            {
                case Left<string, IPAddress> left:
                    Console.Error.WriteLine($"{left.Value} Halting.");
                    Console.Error.Flush();
                    Environment.Exit(1);
                    break;
                
                case Right<string, IPAddress> right:
                    return right.Value;
            }
            
            return null;
        }
    }
}