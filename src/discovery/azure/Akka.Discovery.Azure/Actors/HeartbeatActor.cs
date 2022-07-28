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
            Timers.StartPeriodicTimer(_heartbeatTimerKey, _heartbeat, _heartbeatInterval);
        }

        protected override void PostStop()
        {
            Timers.CancelAll();
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
                    if (!_shutdownCts.IsCancellationRequested)
                    {
                        _log.Warning(f.Cause, "Failed to update TTL heartbeat, retrying");
                        ExecuteUpdateOpWithRetry().PipeTo(Self);
                    }
                    break;
                
                default:
                    Unhandled(message);
                    break;
            }
        }

        private async Task<Status> ExecuteUpdateOpWithRetry()
        {
            // Calculate backoff
            var backoff = new TimeSpan(_backoff.Ticks * _retryCount++);
            // Clamp to maximum backoff time
            backoff = backoff.Min(_maxBackoff);
            
            // Perform backoff delay
            if (backoff > TimeSpan.Zero)
                await Task.Delay(backoff, _shutdownCts.Token);

            if (_shutdownCts.IsCancellationRequested)
                return DefaultFailure;

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token))
            {
                cts.CancelAfter(_timeout);
                if (!await _client.UpdateAsync(cts.Token))
                {
                    return DefaultFailure;
                }
            
                return Status.Success.Instance;
            }
        }

        public ITimerScheduler Timers { get; set; }
    }
}