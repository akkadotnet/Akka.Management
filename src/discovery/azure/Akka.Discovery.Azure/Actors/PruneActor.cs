// -----------------------------------------------------------------------
//  <copyright file="PruneActor.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Threading;
using Akka.Actor;
using Akka.Cluster;
using Akka.Event;

namespace Akka.Discovery.Azure.Actors
{
    internal class PruneActor: UntypedActor, IWithTimers
    {
        public static Props Props(AzureDiscoverySettings settings, ClusterMemberTableClient client)
            => Actor.Props.Create(() => new PruneActor(settings, client)).WithDeploy(Deploy.Local);

        private readonly string _pruneTimerKey = "prune-key";
        private readonly string _prune = "prune";
        
        private readonly ClusterMemberTableClient _client;
        private readonly TimeSpan _pruneInterval;
        private readonly TimeSpan _staleTtlThreshold;
        private readonly ILoggingAdapter _log;
        private readonly CancellationTokenSource _shutdownCts;
        
        private Cluster.Cluster _cluster;
        private Address _selfAddress;
        private bool _pruning;

        public PruneActor(AzureDiscoverySettings settings, ClusterMemberTableClient client)
        {
            _client = client;
            _pruneInterval = settings.PruneInterval;
            _log = Context.GetLogger();
            _shutdownCts = new CancellationTokenSource();

            _staleTtlThreshold = settings.StaleTtlThreshold;
            if (_staleTtlThreshold == TimeSpan.Zero)
                _staleTtlThreshold = new TimeSpan(settings.TtlHeartbeatInterval.Ticks * 5);
        }

        protected override void PreStart()
        {
            _cluster = Cluster.Cluster.Get(Context.System);
            _selfAddress = _cluster.SelfAddress;
            _cluster.Subscribe(Self, typeof(ClusterEvent.LeaderChanged), typeof(ClusterEvent.RoleLeaderChanged));
        }

        protected override void PostStop()
        {
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
            Timers.CancelAll();
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case string str when str == _prune:
                    if (_pruning)
                        break;
                    
                    _pruning = true;
                    if(_log.IsDebugEnabled)
                        _log.Debug("Pruning stale cluster member entries");
                    
                    _client.PruneAsync((DateTime.UtcNow - _staleTtlThreshold).Ticks, _shutdownCts.Token)
                        .PipeTo(Self, success: _ => Done.Instance);
                    break;
                
                case ClusterEvent.LeaderChanged evt:
                    CheckTimerRequirements(evt.Leader == _selfAddress);
                    break;
                
                case ClusterEvent.RoleLeaderChanged evt:
                    CheckTimerRequirements(evt.Leader == _selfAddress);
                    break;
                
                case Done _:
                    _pruning = false;
                    break;
                
                case Status.Failure f:
                    _pruning = false;
                    _log.Warning(f.Cause, "Failed to prune stale cluster member entries");
                    break;
                
                default:
                    Unhandled(message);
                    break;
            }
        }

        private void CheckTimerRequirements(bool isLeader)
        {
            if (isLeader && !Timers.IsTimerActive(_pruneTimerKey))
            {
                // We just received leader status
                Timers.StartPeriodicTimer(_pruneTimerKey, _prune, _pruneInterval);
            } 
            else if (!isLeader && Timers.IsTimerActive(_pruneTimerKey))
            {
                // we lost leader status
                Timers.Cancel(_pruneTimerKey);
            }
        }

        public ITimerScheduler Timers { get; set; }
    }
}