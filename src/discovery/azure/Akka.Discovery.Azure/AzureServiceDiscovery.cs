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

        public AzureServiceDiscovery(ExtendedActorSystem system)
        {
            _system = system;
            _log = Logging.GetLogger(system, typeof(AzureServiceDiscovery));
            
            _system.Settings.InjectTopLevelFallback(DefaultConfig);
            _settings = AzureDiscoverySettings.Create(system);
            
            var setup = _system.Settings.Setup.Get<AzureDiscoverySetup>();
            if (setup.HasValue)
                _settings = setup.Value.Apply(_settings);

            _guardianActor = system.SystemActorOf(AzureDiscoveryGuardian.Props(_settings), "azure-discovery-guardian");

            var shutdown = CoordinatedShutdown.Get(system);
            shutdown.AddTask(CoordinatedShutdown.PhaseClusterExiting, "stop-azure-discovery", () =>
            {
                if(_log.IsDebugEnabled)
                    _log.Debug("Service stopped");
                _guardianActor.Tell(PoisonPill.Instance);
                return Task.FromResult(Done.Instance);
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