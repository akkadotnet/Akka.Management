using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kubernetes.StressTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddLogging()
                        .AddSingleton<AkkaService>()
                        .AddHostedService<AkkaService>(); // runs Akka.NET
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