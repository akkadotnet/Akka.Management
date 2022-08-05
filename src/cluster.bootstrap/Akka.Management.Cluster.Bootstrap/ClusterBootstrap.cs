//-----------------------------------------------------------------------
// <copyright file="ClusterBootstrap.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Event;
using Akka.Http.Dsl;
using Akka.Management.Cluster.Bootstrap.ContactPoint;
using Akka.Management.Cluster.Bootstrap.Internal;
using Akka.Util;

namespace Akka.Management.Cluster.Bootstrap
{
    public class ClusterBootstrap : IExtension, IManagementRouteProvider
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
        internal ClusterBootstrapSettings Settings { get; }
        internal Lazy<ServiceDiscovery> Discovery { get; }
        private readonly IJoinDecider _joinDecider;

        private readonly TaskCompletionSource<Uri> _selfContactPointTcs;

        internal Task<Uri> SelfContactPoint => _selfContactPointTcs.Task; 

        public ClusterBootstrap(ExtendedActorSystem system)
        {
            _selfContactPointTcs = new TaskCompletionSource<Uri>();
            
            _system = system;
            _system.Settings.InjectTopLevelFallback(DefaultConfiguration());
            
            _log = Logging.GetLogger(system, typeof(ClusterBootstrap));
            _bootstrapStep= new AtomicReference<Internal.IBootstrapStep>(Internal.NotRunning.Instance);
            Settings = ClusterBootstrapSettings.Create(system.Settings.Config, _log);
            
            var setup = _system.Settings.Setup.Get<ClusterBootstrapSetup>().Value ?? new ClusterBootstrapSetup();

            var contactPointDiscovery = _system.Settings.Setup.Get<ContactPointDiscoverySetup>().Value;
            if (contactPointDiscovery != null)
                setup.ContactPointDiscovery = contactPointDiscovery;

            var contactPoint = _system.Settings.Setup.Get<ContactPointSetup>().Value;
            if (contactPoint != null)
                setup.ContactPoint = contactPoint;

            var joinDecider = _system.Settings.Setup.Get<JoinDeciderSetup>().Value;
            if (joinDecider != null)
                setup.JoinDecider = joinDecider;

            Settings = setup.Apply(Settings); 

            Discovery = new Lazy<ServiceDiscovery>(() =>
            {
                var method = Settings.ContactPointDiscovery.DiscoveryMethod; 
                if (method == "akka.discovery")
                {
                    var discovery = Akka.Discovery.Discovery.Get(system).Default;
                    _log.Info("Bootstrap using default `akka.discovery` method: {0}", Logging.SimpleName(discovery));
                    return discovery;
                }

                _log.Info("Bootstrap using `akka.discovery` method: {0}", method);
                return Akka.Discovery.Discovery.Get(system).LoadServiceDiscovery(method);
            });
            
            var joinDeciderType = Type.GetType(Settings.JoinDecider.ImplClass);
            if (joinDeciderType == null)
                throw new ConfigurationException(
                    $"Could not convert FQCN name into concrete type: [{Settings.JoinDecider.ImplClass}]");

            _joinDecider = (IJoinDecider)Activator.CreateInstance(joinDeciderType, system, Settings);
            
            var autoStart = system.Settings.Config.GetStringList("akka.extensions")
                .Any(s => s.Contains(typeof(ClusterBootstrap).Name));
            if (autoStart)
            {
                _log.Info("ClusterBootstrap loaded through 'akka.extensions' auto starting bootstrap.");
                // Akka Management hosts the HTTP routes used by bootstrap
                // we can't let it block extension init, so run it in a different thread and let constructor complete
                Task.Run(async () =>
                {
                    try
                    {
                        await AkkaManagement.Get(system).Start();
                        await ClusterBootstrap.Get(system).Start();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e, "Failed to autostart cluster bootstrap, terminating system");
                        await system.Terminate();
                    }
                });
            }
        }

        internal void SetSelfContactPoint(Uri baseUri)
        {
            _selfContactPointTcs.SetResult(baseUri);
        }

        private void EnsureSelfContactPoint()
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                if (!SelfContactPoint.IsCompleted)
                {
                    _selfContactPointTcs.SetException(new TaskCanceledException("Awaiting ClusterBootstrap.SelfContactPointUri timed out."));
                    _log.Error("'Bootstrap.selfContactPoint' was NOT set, but is required for the bootstrap to work " +
                               "if binding bootstrap routes manually and not via akka-management.");
                }
            });
        }
        
        public async Task Start()
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
                    Settings.ContactPointDiscovery.DiscoveryMethod);
                
                EnsureSelfContactPoint();
                var bootstrapProps = BootstrapCoordinator.Props(Discovery.Value, _joinDecider, Settings);
                var bootstrap = _system.SystemActorOf(bootstrapProps, "bootstrapCoordinator");
                
                // Bootstrap already logs in several other execution points when it can't form a cluster, and why.
                if (!SelfContactPoint.IsCompleted)
                    await SelfContactPoint;
                
                var uri = SelfContactPoint.Result;
                bootstrap.Tell(new BootstrapCoordinator.Protocol.InitiateBootstrapping(uri));
                
                return;
            }

            _log.Warning("Bootstrap already initiated, yet Start() method was called again. Ignoring.");
        }

        public Route[] Routes(ManagementRouteProviderSettings routeProviderSettings)
        {
            _log.Info($"Using self contact point address: {routeProviderSettings.SelfBaseUri}");
            SetSelfContactPoint(routeProviderSettings.SelfBaseUri);

            return new HttpClusterBootstrapRoutes(Settings).Routes;
        }
    }
    
    public class ClusterBootstrapProvider : ExtensionIdProvider<ClusterBootstrap>
    {
        public override ClusterBootstrap CreateExtension(ExtendedActorSystem system)
            => new ClusterBootstrap(system);
    }
}