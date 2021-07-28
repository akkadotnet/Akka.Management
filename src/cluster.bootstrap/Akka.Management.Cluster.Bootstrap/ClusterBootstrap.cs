using System;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Event;
using Akka.Management.Cluster.Bootstrap.Internal;
using Akka.Util;

namespace Akka.Management.Cluster.Bootstrap
{
    public class ClusterBootstrap : IExtension
    {
        internal static class Internal
        {
            public interface IBootstrapStep { }
            public class NotRunning : IBootstrapStep
            {
                public static readonly NotRunning Instance = new NotRunning();
                private NotRunning() { }
            }
            public class Initializing : IBootstrapStep
            {
                public static readonly Initializing Instance = new Initializing();
                private Initializing() { }
            }
        }

        public static Config DefaultConfiguration()
            => ConfigurationFactory.FromResource<ClusterBootstrap>("Akka.Management.Cluster.Bootstrap.reference.conf");

        public static ClusterBootstrap Get(ActorSystem system)
            => system.WithExtension<ClusterBootstrap, ClusterBootstrapProvider>();

        private readonly ExtendedActorSystem _system;
        private readonly ILoggingAdapter _log;
        private readonly AtomicReference<Internal.IBootstrapStep> _bootstrapStep; 
        private readonly ClusterBootstrapSettings _settings;
        private readonly Lazy<ServiceDiscovery> _discovery;
        private readonly IJoinDecider _joinDecider;

        public ClusterBootstrap(ExtendedActorSystem system)
        {
            _system = system;
            _log = Logging.GetLogger(system, typeof(ClusterBootstrap));
            _bootstrapStep= new AtomicReference<Internal.IBootstrapStep>(Internal.NotRunning.Instance);
            _settings = new ClusterBootstrapSettings(system.Settings.Config, _log);

            _discovery = new Lazy<ServiceDiscovery>(() =>
            {
                var method = _settings.ContactPointDiscovery.DiscoveryMethod; 
                if (method == "akka.discovery")
                {
                    var discovery = Discovery.Discovery.Get(system).Default;
                    _log.Info("Bootstrap using default `akka.discovery` method: {0}", Logging.SimpleName(discovery));
                    return discovery;
                }

                _log.Info("Bootstrap using `akka.discovery` method: {}", method);
                return Discovery.Discovery.Get(system).LoadServiceDiscovery(method);
            });
            
            var joinDeciderType = Type.GetType(_settings.JoinDecider.ImplClass);
            if (joinDeciderType == null)
                throw new ConfigurationException(
                    $"Could not convert FQCN name into concrete type: [{_settings.JoinDecider.ImplClass}]");

            _joinDecider = (IJoinDecider)Activator.CreateInstance(joinDeciderType, system, _settings);
            
            var autoStart = system.Settings.Config.GetStringList("akka.extensions")
                .Any(s => s.Contains(typeof(ClusterBootstrap).Name));
            if (autoStart)
            {
                _log.Info("ClusterBootstrap loaded through 'akka.extensions' auto starting bootstrap.");
                Get(system).Start();
            }
        }

        public void Start()
        {
            var settingsSeedNodes = Akka.Cluster.Cluster.Get(_system).Settings.SeedNodes; 
            if (!settingsSeedNodes.IsEmpty)
            {
                _log.Warning(
                    "Application is configured with specific `akka.cluster.seed-nodes`: [{0}], bailing out of the bootstrap process! " +
                    "If you want to use the automatic bootstrap mechanism, make sure to NOT set explicit seed nodes in the configuration. " +
                    "This node will attempt to join the configured seed nodes.",
                    string.Join(", ", settingsSeedNodes));
                return;
            }

            if (_bootstrapStep.CompareAndSet(Internal.NotRunning.Instance, Internal.Initializing.Instance))
            {
                _log.Info("Initiating bootstrap procedure using {0} method...",
                    _settings.ContactPointDiscovery.DiscoveryMethod);
                var bootstrapProps = BootstrapCoordinator.Props(_discovery.Value, _joinDecider, _settings);
                var bootstrap = _system.SystemActorOf(bootstrapProps, "bootstrapCoordinator");
                
                bootstrap.Tell(BootstrapCoordinator.Protocol.InitiateBootstrapping.Instance);
                return;
            }

            _log.Warning("Bootstrap already initiated, yet start() method was called again. Ignoring.");
        }
    }
    
    public class ClusterBootstrapProvider : ExtensionIdProvider<ClusterBootstrap>
    {
        public override ClusterBootstrap CreateExtension(ExtendedActorSystem system)
            => new ClusterBootstrap(system);
    }
}