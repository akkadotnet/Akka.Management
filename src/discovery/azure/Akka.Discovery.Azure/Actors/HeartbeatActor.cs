// -----------------------------------------------------------------------
//  <copyright file="HeartbeatActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Util.Internal;

namespace Akka.Discovery.Azure.Actors
{
    /// <summary>
    /// Manages the TTL heartbeat that updates the Azure discovery table row entry for this cluster node.
    /// Instantiated as a child of the AzureDiscoveryGuardian actor, only after it initialized properly.
    /// Heartbeat is based on the akka.discovery.azure.ttl-heartbeat-interval setting, which defaults to 1 minute.
    /// </summary>
    internal sealed class HeartbeatActor: UntypedActor, IWithTimers
    {
        public static Props Props(AzureDiscoverySettings settings, ClusterMemberTableClient client)
            => Actor.Props.Create(() => new HeartbeatActor(settings, client)).WithDeploy(Deploy.Local);

        private static readonly Status.Failure DefaultFailure = new Status.Failure(null);
        
        private readonly string _heartbeatTimerKey = "heartbeat-key";
        private readonly string _heartbeat = "heartbeat";
        private readonly ILoggingAdapter _log;
        private readonly ClusterMemberTableClient _client;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _heartbeatInterval;
        private readonly CancellationTokenSource _shutdownCts;

        private readonly TimeSpan _backoff;
        private readonly TimeSpan _maxBackoff;
        private int _retryCount;
        private bool _updating;

        public HeartbeatActor(AzureDiscoverySettings settings, ClusterMemberTableClient client)
        {
            _client = client;
            _timeout = settings.OperationTimeout;
            _backoff = settings.RetryBackoff;
            _maxBackoff = settings.MaximumRetryBackoff;
            _heartbeatInterval = settings.TtlHeartbeatInterval;
            _log = Context.GetLogger();
            _shutdownCts = new CancellationTokenSource();
        }

        protected override void PreStart()
        {
            Timers!.StartPeriodicTimer(_heartbeatTimerKey, _heartbeat, _heartbeatInterval);
        }

        protected override void PostStop()
        {
            Timers!.CancelAll();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case string str when str == _heartbeat:
                    if(_updating)
                        break;
                    
                    _updating = true;
                    _retryCount = 0;
                    if(_log.IsDebugEnabled)
                        _log.Debug("Updating cluster member entry TTL");

                    ExecuteUpdateOpWithRetry().PipeTo(Self);
                    break;
                
                case Status.Success _:
                    _updating = false;
                    break;
                
                case Status.Failure f:
                    if (_shutdownCts.IsCancellationRequested)
                    {
                        _log.Warning(f.Cause, "Failed to prune stale cluster member entries");
                        return;
                    }
                    
                    _log.Warning(f.Cause, "Failed to update TTL heartbeat, retrying");
                    ExecuteUpdateOpWithRetry().PipeTo(Self);
                    break;
                
                default:
                    Unhandled(message);
                    break;
            }
        }

        // Always call this method using PipeTo, we'll be waiting for Status.Success or Status.Failure asynchronously
        private async Task ExecuteUpdateOpWithRetry()
        {
            // Calculate backoff
            var backoff = new TimeSpan(_backoff.Ticks * _retryCount++);
            // Clamp to maximum backoff time
            backoff = backoff.Min(_maxBackoff);
            
            // Perform backoff delay
            if (backoff > TimeSpan.Zero)
                await Task.Delay(backoff, _shutdownCts.Token);

            _shutdownCts.Token.ThrowIfCancellationRequested();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
            cts.CancelAfter(_timeout);
            await _client.UpdateAsync(cts.Token);
        }

        public ITimerScheduler? Timers { get; set; }
    }
}