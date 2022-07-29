using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Bootstrap.Docker;
using Akka.Cluster;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.DependencyInjection;
using Akka.Discovery.Azure;
using Akka.Event;
using Akka.Util;
using Akka.Util.Internal;
using AzureCluster.Cmd;
using Microsoft.Extensions.Hosting;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace AzureCluster
{
    public sealed class ChaosActor : ReceiveActor
    {
        public ChaosActor()
        {
            var log = Context.GetLogger();
            ReceiveAsync<int>(async i =>
            {
                switch (i)
                {
                    case 1: // graceful shutdown
                        log.Error("======== Shutting down gracefully ========");
                        await Task.Delay(100);
                        await Context.System.Terminate();
                        return;
                    case 2: // crash
                        log.Error("======== Crashing system ========");
                        await Task.Delay(100);
                        Context.System.AsInstanceOf<ExtendedActorSystem>().Abort();
                        return;
                }
            });
        }
    }
    
    public class Subscriber : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public Subscriber()
        {
            var mediator = DistributedPubSub.Get(Context.System).Mediator;

            // subscribe to the topic named "content"
            mediator.Tell(new Subscribe("content", Self));

            Receive<int>(s =>
            {
                _log.Info($"Got {s}");
                if (s % 2 == 0)
                {
                    mediator.Tell(new Publish("content", ThreadLocalRandom.Current.Next(0,10)));
                }
            });

            Receive<SubscribeAck>(subscribeAck =>
            {
                if (subscribeAck.Subscribe.Topic.Equals("content")
                    && subscribeAck.Subscribe.Ref.Equals(Self)
                    && subscribeAck.Subscribe.Group == null)
                {
                    _log.Info("subscribing");
                }
            });
        }
    }
    
    public class AkkaService: IHostedService
    {
        private const string AzuriteConnectionString = 
            "DefaultEndpointsProtocol=http;" +
            "AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            "BlobEndpoint=http://{0}:10000/devstoreaccount1;" +
            "QueueEndpoint=http://{0}:10001/devstoreaccount1;" +
            "TableEndpoint=http://{0}:10002/devstoreaccount1;";
        
        private ActorSystem? _system;
        private readonly IServiceProvider _serviceProvider;

        // needed to help guarantee clean shutdowns
        private readonly IHostApplicationLifetime _lifetime;

        public AkkaService(IServiceProvider serviceProvider, IHostApplicationLifetime lifetime)
        {
            _serviceProvider = serviceProvider;
            _lifetime = lifetime;
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var config = ConfigurationFactory.ParseString(File.ReadAllText("app.conf"))
                .BootstrapFromDocker();

            var bootstrap = BootstrapSetup.Create()
                .WithConfig(config) // load HOCON
                .WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster

            // enable DI support inside this ActorSystem, if needed
            var diSetup = DependencyResolverSetup.Create(_serviceProvider);

            var azuriteHost = Environment.GetEnvironmentVariable("AZURITE_HOST")?.Trim() ?? "azurite";
            var connectionString = string.Format(AzuriteConnectionString, azuriteHost);
            var azureSetup = new AzureDiscoverySetup()
                .WithConnectionString(connectionString);
            
            // merge this setup (and any others) together into ActorSystemSetup
            var actorSystemSetup = bootstrap.And(azureSetup).And(diSetup);
            
            var systemName = Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim() ?? "AkkaService";

            _system = ActorSystem.Create(systemName, actorSystemSetup);

            // start https://cmd.petabridge.com/ for diagnostics and profit
            var pbm = PetabridgeCmd.Get(_system); // start Pbm
            pbm.RegisterCommandPalette(ClusterCommands.Instance);
            pbm.RegisterCommandPalette(new RemoteCommands());
            pbm.RegisterCommandPalette(new TestCommands());
            pbm.Start(); // begin listening for PBM management commands
            
            _system.ActorOf(ClusterListener.Props(), "listener");

            var useChaos = Environment.GetEnvironmentVariable("USE_CHAOS")?.Trim().ToLowerInvariant();
            if (useChaos is "true")
            {
                var chaos = _system.ActorOf(Props.Create<ChaosActor>(), "chaos");
                Cluster.Get(_system).RegisterOnMemberUp(() =>
                {
                    _system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () =>
                    {
                        chaos.Tell(ThreadLocalRandom.Current.Next(0,200));
                    });
                });
            }

            var usePubSub = Environment.GetEnvironmentVariable("USE_PUBSUB")?.Trim().ToLowerInvariant();
            if (usePubSub is "true")
            {
                var mediator = DistributedPubSub.Get(_system).Mediator;
                var subscriber = _system.ActorOf(Props.Create(() => new Subscriber()), "subscriber");
                Cluster.Get(_system).RegisterOnMemberUp(() =>
                {
                    _system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () =>
                    {
                        mediator.Tell(new Publish("content", ThreadLocalRandom.Current.Next(0,10)));
                    });
                });
            }

            _system.WhenTerminated.ContinueWith(tr =>
            {
                _lifetime.StopApplication(); // when the ActorSystem terminates, terminate the process
            });

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if(_system != null)
                await _system.Terminate();
            //await CoordinatedShutdown.Get(System).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }
    }
}