// -----------------------------------------------------------------------
//  <copyright file="RedisServiceDiscovery.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Redis.Actors;
using Akka.Event;
using Akka.Util.Internal;

namespace Akka.Discovery.Redis
{
    /// <summary>
    /// Redis-based service discovery implementation.
    /// The Redis connection is owned and established lazily by the guardian actor, so constructing
    /// this discovery (and therefore the ActorSystem) never blocks on or fails because of an
    /// unreachable Redis instance.
    /// </summary>
    public class RedisServiceDiscovery : ServiceDiscovery
    {
        private static readonly AtomicCounter NextGuardianId = new(1);

        internal const string DefaultPath = "redis";
        internal const string DefaultConfigPath = "akka.discovery." + DefaultPath;

        /// <summary>
        /// Gets the full configuration path for a Redis discovery method
        /// </summary>
        internal static string FullPath(string path) => $"akka.discovery.{path}";

        private readonly ILoggingAdapter _log;
        private readonly ExtendedActorSystem _system;
        private readonly RedisDiscoverySettings _settings;
        private readonly IActorRef _guardianActor;

        /// <summary>
        /// Creates a new RedisServiceDiscovery instance
        /// </summary>
        public RedisServiceDiscovery(ExtendedActorSystem system)
            : this(system, system.Settings.Config.GetConfig(DefaultConfigPath))
        {
        }

        /// <summary>
        /// Creates a new RedisServiceDiscovery instance with custom configuration
        /// </summary>
        public RedisServiceDiscovery(ExtendedActorSystem system, Configuration.Config config)
        {
            _system = system;
            _log = Logging.GetLogger(system, typeof(RedisServiceDiscovery));

            var fullConfig = config.WithFallback(RedisDiscovery.DefaultConfiguration().GetConfig(DefaultConfigPath));
            _settings = RedisDiscoverySettings.Create(system, fullConfig);

            var guardianId = NextGuardianId.GetAndIncrement();
            _guardianActor = system.SystemActorOf(
                RedisDiscoveryGuardian.Props(_settings),
                $"redis-discovery-guardian-{guardianId}");

            var shutdown = CoordinatedShutdown.Get(system);
            shutdown.AddTask(CoordinatedShutdown.PhaseClusterExiting, $"stop-redis-discovery-{guardianId}", async () =>
            {
                try
                {
                    await _guardianActor.Ask<global::Akka.Done>(StopDiscovery.Instance);
                }
                catch
                {
                    _guardianActor.Tell(PoisonPill.Instance);
                    // Just ignore any timeout exceptions, if we failed to remove ourself from Redis,
                    // the entry will expire automatically based on TTL.
                }

                if (_log.IsDebugEnabled)
                    _log.Debug("Service stopped");

                return global::Akka.Done.Instance;
            });

            if (_log.IsDebugEnabled)
                _log.Debug("Service started");
        }

        /// <inheritdoc/>
        public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
        {
            if (_log.IsDebugEnabled)
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
