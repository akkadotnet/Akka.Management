// -----------------------------------------------------------------------
//  <copyright file="AzureServiceDiscovery.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Azure.Actors;
using Akka.Discovery.Azure.Model;
using Akka.Event;
using Akka.Util;

namespace Akka.Discovery.Azure
{
    public class AzureServiceDiscovery : ServiceDiscovery
    {
        public static readonly Configuration.Config DefaultConfig = 
            ConfigurationFactory.FromResource<AzureServiceDiscovery>("Akka.Discovery.Azure.reference.conf");
        
        private readonly ILoggingAdapter _log;
        private readonly ExtendedActorSystem _system;
        private readonly AzureDiscoverySettings _settings;

        private readonly IActorRef _guardianActor;

        // Backward compatibility constructor
        public AzureServiceDiscovery(ExtendedActorSystem system)
            : this(system, system.Settings.Config.GetConfig("akka.discovery.azure"))
        {
        }
        
        public AzureServiceDiscovery(ExtendedActorSystem system, Configuration.Config config)
        {
            _system = system;
            _log = Logging.GetLogger(system, typeof(AzureServiceDiscovery));
            
            var fullConfig = config.WithFallback(DefaultConfig.GetConfig("akka.discovery.azure"));
            _settings = AzureDiscoverySettings.Create(system, fullConfig);
            
            var setup = _system.Settings.Setup.Get<AzureDiscoverySetup>();
            if (setup.HasValue)
                _settings = setup.Value.Apply(_settings);

            _guardianActor = system.SystemActorOf(AzureDiscoveryGuardian.Props(_settings), "azure-discovery-guardian");

            var shutdown = CoordinatedShutdown.Get(system);
            shutdown.AddTask(CoordinatedShutdown.PhaseClusterExiting, "stop-azure-discovery", async () =>
            {
                try
                {
                    await _guardianActor.Ask<Done>(StopDiscovery.Instance);
                }
                catch
                {
                    _guardianActor.Tell(PoisonPill.Instance);
                    // Just ignore any timeout exceptions, if we failed to remove ourself from the member entry list,
                    // the entry will be removed in future entry pruning.
                }
                
                if(_log.IsDebugEnabled)
                    _log.Debug("Service stopped");
                
                return Done.Instance;
            });
            
            if(_log.IsDebugEnabled)
                _log.Debug("Service started");
        }
        
        public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
        {
            if(_log.IsDebugEnabled)
                _log.Debug("Starting lookup for service {0}", lookup.ServiceName);

            try
            {
                var members = await _guardianActor.Ask<ImmutableList<ClusterMember>>(lookup, resolveTimeout);

                return new Resolved(
                    lookup.ServiceName,
                    members.Select(m => new ResolvedTarget(m.Host, m.Port, m.Address)).ToImmutableList());
            }
            catch (Exception e)
            {
                _log.Warning(e, "Failed to perform contact point lookup");
                return new Resolved(lookup.ServiceName);
            }
        }
    }
}