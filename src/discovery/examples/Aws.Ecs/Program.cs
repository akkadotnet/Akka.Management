using System;
using System.Net;
using Akka.Cluster.Hosting;
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

            var clusterName = Environment.GetEnvironmentVariable("AKKA__DISCOVERY__AWS_API_ECS__CLUSTER");
            clusterName ??= "ecs-integration-test-app";
            
            var serviceName = Environment.GetEnvironmentVariable("AKKA__MANAGEMENT__CLUSTER__BOOTSTRAP__CONTACT_POINT_DISCOVERY__SERVICE_NAME");
            serviceName ??= "ecs-integration-test-app";
            
            var host = new HostBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddAkka("ecsBootstrapDemo", (builder, provider) =>
                    {
                        builder
                            .WithRemoting(privateAddress.ToString(), 4053)
                            .WithClustering()
                            .WithAkkaManagement(setup =>
                            {
                                setup.Http.Port = 8558;
                                setup.Http.Hostname = privateAddress.ToString();
                            })
                            .WithClusterBootstrap(setup =>
                            {
                                setup.ContactPoint.FallbackPort = 8558;
                                setup.ContactPointDiscovery.ServiceName = serviceName;
                            })
                            .WithAwsEcsDiscovery(clusterName: clusterName);
                    });
                }).Build();
            
            host.Run();
        }

        private static IPAddress GetPrivateAddressOrExit()
        {
            switch (AwsEcsDiscovery.GetContainerAddress())
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