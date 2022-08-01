// -----------------------------------------------------------------------
//  <copyright file="PruneActor.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using Akka.Util.Internal;
using DotNetty.Common.Utilities;

namespace Akka.Discovery.Azure.Actors
{
    /// <summary>
    /// Manages Azure discovery table stale entries pruning.
    /// Instantiated as a child of the AzureDiscoveryGuardian actor, only after it initialized properly.
    /// Pruning timer is activated when the node became the cluster leader.
    /// Prune interval is based on the akka.discovery.azure.prune-interval setting, which defaults to 1 hour.
    /// </summary>
    internal class PruneActor: UntypedActor, IWithTimers
    {
        public static Props Props(AzureDiscoverySettings settings, ClusterMemberTableClient client)
            => Actor.Props.Create(() => new PruneActor(settings, client)).WithDeploy(Deploy.Local);

        private static readonly Status.Failure DefaultFailure = new Status.Failure(null);
        
        private readonly string _pruneTimerKey = "prune-key";
        private readonly string _prune = "prune";
        
        private readonly ClusterMemberTableClient _client;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _pruneInterval;
        private readonly TimeSpan _staleTtlThreshold;
        private readonly ILoggingAdapter _log;
        private readonly CancellationTokenSource _shutdownCts;
        private CancellationTokenSource _leaderCts;
        
        private Cluster.Cluster _cluster;
        private Address _selfAddress;
        
        private readonly TimeSpan _backoff;
        private readonly TimeSpan _maxBackoff;
        private int _retryCount;
        private bool _pruning;

        public PruneActor(AzureDiscoverySettings settings, ClusterMemberTableClient client)
        {
            _client = client;
            _timeout = settings.OperationTimeout;
            _backoff = settings.RetryBackoff;
            _maxBackoff = settings.MaximumRetryBackoff;
            _pruneInterval = settings.PruneInterval;
            _log = Context.GetLogger();
            _shutdownCts = new CancellationTokenSource();

            _staleTtlThreshold = settings.EffectiveStaleTtlThreshold;
            
            Become(WaitingForLeadership);
        }

        protected override void PreStart()
        {
            _cluster = Cluster.Cluster.Get(Context.System);
            _selfAddress = _cluster.SelfAddress;
            _cluster.Subscribe(Self, typeof(ClusterEvent.LeaderChanged));
        }

        protected override void PostStop()
        {
            Timers.CancelAll();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
        }

        private bool WaitingForLeadership(object message)
        {
            switch (message)
            {
                case ClusterEvent.LeaderChanged evt:
                    if(evt.Leader == _selfAddress)
                    {
                        // We just received leader status
                        Timers.StartPeriodicTimer(_pruneTimerKey, _prune, _pruneInterval);
                        _leaderCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
                        Become(Pruning);
                    }
                    return true;
                
                default:
                    return false;
            }
        }

        private bool Pruning(object message)
        {
            switch (message)
            {
                case string str when str == _prune:
                    if (_pruning)
                        return true;

                    _pruning = true;
                    _retryCount = 0;
                    if (_log.IsDebugEnabled)
                        _log.Debug("Pruning stale cluster member entries");

                    ExecutePruneOpWithRetry().PipeTo(Self);
                    return true;
                
                case ClusterEvent.LeaderChanged evt:
                    if (evt.Leader != _selfAddress)
                    {
                        // we lost leader status
                        Timers.CancelAll();
                        _leaderCts.Cancel();
                        _leaderCts.Dispose();
                        Become(WaitingForLeadership);
                    }
                    return true;
                
                case Done _:
                    _pruning = false;
                    return true;
                
                case Status.Failure f:
                    if (_shutdownCts.IsCancellationRequested || _leaderCts.IsCancellationRequested)
                        return true;
                    
                    _log.Warning(f.Cause, "Failed to prune stale cluster member entries, retrying");
                    ExecutePruneOpWithRetry().PipeTo(Self);
                    return true;
                
                default:
                    return false;
            }
        }
        
        protected override void OnReceive(object message)
        {
            throw new NotImplementedException("Should never reach this code");
        }

        private async Task<Status> ExecutePruneOpWithRetry()
        {
            // Calculate backoff
            var backoff = new TimeSpan(_backoff.Ticks * _retryCount++);
            // Clamp to maximum backoff time
            backoff = backoff.Min(_maxBackoff);
            
            // Perform backoff delay
            if (backoff > TimeSpan.Zero)
                await Task.Delay(backoff, _leaderCts.Token);

            if (_leaderCts.IsCancellationRequested)
                return DefaultFailure;

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_leaderCts.Token))
            {
                cts.CancelAfter(_timeout);
                if (!await _client.PruneAsync((DateTime.UtcNow - _staleTtlThreshold).Ticks, cts.Token))
                {
                    return DefaultFailure;
                }
            
                return Status.Success.Instance;
            }
        }

        public ITimerScheduler Timers { get; set; }
    }
}