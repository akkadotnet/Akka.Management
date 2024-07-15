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
        internal const string DefaultPath = "azure";
        internal const string DefaultConfigPath = "akka.discovery." + DefaultPath;
        internal static string FullPath(string path) => $"akka.discovery.{path}";
        
        private readonly ILoggingAdapter _log;
        private readonly ExtendedActorSystem _system;
        private readonly AzureDiscoverySettings _settings;

        private readonly IActorRef _guardianActor;

        // Backward compatibility constructor
        public AzureServiceDiscovery(ExtendedActorSystem system)
            : this(system, system.Settings.Config.GetConfig(DefaultConfigPath))
        {
        }
        
        public AzureServiceDiscovery(ExtendedActorSystem system, Configuration.Config config)
        {
            _system = system;
            _log = Logging.GetLogger(system, typeof(AzureServiceDiscovery));
            
            var fullConfig = config.WithFallback(AzureDiscovery.DefaultConfiguration().GetConfig(DefaultConfigPath));
            _settings = AzureDiscoverySettings.Create(system, fullConfig);
            
            var setup = _system.Settings.Setup.Get<AzureDiscoverySetup>();
            if (setup.HasValue)
                _settings = setup.Value.Apply(_settings);

            // We're cheating here, `discovery-id` setting doesn't officially exist in the official
            // default HOCON settings, this is a marker we injected from the Akka.Hosting extension
            // to map HOCON subsection with its related Setup.
            var id = fullConfig.GetString("discovery-id");
            if (id is not null)
            {
                var multiSetup = _system.Settings.Setup.Get<AzureDiscoveryMultiSetup>();
                if (multiSetup.HasValue && multiSetup.Value.Setups.TryGetValue(id, out var configSetup))
                    _settings = configSetup.Apply(_settings);
            }

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