using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.KubernetesApi;
using Akka.Management.Cluster.Bootstrap;
using Akka.Management.Dsl;

namespace HoconKubernetesCluster;

public static class Program
{
    public static async Task Main(string[] args)
    {
        #region Console shutdown setup
            
        var exitEvent = new ManualResetEvent(false);
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            exitEvent.Set();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            exitEvent.Set();
        };
            
        #endregion
        
        var hocon = await File.ReadAllTextAsync("HOCON.conf");
        var config = EnvironmentSettings.Create().ToConfig()
            .WithFallback(ConfigurationFactory.ParseString(hocon))
            .WithFallback(AkkaManagementProvider.DefaultConfiguration())
            .WithFallback(ClusterBootstrap.DefaultConfiguration())
            .WithFallback(KubernetesDiscovery.DefaultConfiguration());

        var actorSystem = ActorSystem.Create("hocon-cluster-discovery", config);
        
        exitEvent.WaitOne();
        await actorSystem.Terminate();
    }
}