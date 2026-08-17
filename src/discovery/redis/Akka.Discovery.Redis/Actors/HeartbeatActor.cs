// -----------------------------------------------------------------------
//  <copyright file="HeartbeatActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;

namespace Akka.Discovery.Redis.Actors
{
    /// <summary>
    /// Manages the TTL heartbeat that refreshes the Redis entry for this cluster node.
    /// Instantiated as a child of the <see cref="RedisDiscoveryGuardian"/> actor. On a failed update
    /// the actor does not busy-retry; it waits for the next periodic tick, which naturally bounds
    /// retry pressure to the heartbeat interval.
    /// </summary>
    internal sealed class HeartbeatActor : UntypedActor, IWithTimers
    {
        /// <summary>
        /// Creates Props for the HeartbeatActor
        /// </summary>
        public static Props Props(RedisDiscoverySettings settings, ClusterMemberRedisClient client)
            => global::Akka.Actor.Props.Create(() => new HeartbeatActor(settings, client)).WithDeploy(Deploy.Local);

        private readonly string _heartbeatTimerKey = "heartbeat-key";
        private readonly string _heartbeat = "heartbeat";
        private readonly ILoggingAdapter _log;
        private readonly ClusterMemberRedisClient _client;
        private readonly TimeSpan _heartbeatInterval;
        private readonly TimeSpan _operationTimeout;
        private readonly CancellationTokenSource _shutdownCts;

        private bool _updating;

        /// <summary>
        /// Creates a new HeartbeatActor
        /// </summary>
        public HeartbeatActor(RedisDiscoverySettings settings, ClusterMemberRedisClient client)
        {
            _client = client;
            _heartbeatInterval = settings.TtlHeartbeatInterval;
            _operationTimeout = settings.OperationTimeout;
            _log = Context.GetLogger();
            _shutdownCts = new CancellationTokenSource();
        }

        /// <inheritdoc/>
        protected override void PreStart()
        {
            Timers!.StartPeriodicTimer(_heartbeatTimerKey, _heartbeat, _heartbeatInterval);
        }

        /// <inheritdoc/>
        protected override void PostStop()
        {
            Timers!.CancelAll();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
        }

        /// <inheritdoc/>
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case string str when str == _heartbeat:
                    if (_updating)
                        break;

                    _updating = true;
                    if (_log.IsDebugEnabled)
                        _log.Debug("Updating cluster member entry TTL");

                    ExecuteUpdateAsync().PipeTo(Self);
                    break;

                case Status.Success _:
                    _updating = false;
                    break;

                case Status.Failure f:
                    // Do not busy-retry: clear the flag and let the next periodic tick try again.
                    // This bounds retry pressure to the heartbeat interval instead of spinning.
                    _updating = false;

                    if (_shutdownCts.IsCancellationRequested)
                    {
                        _log.Warning(f.Cause, "Failed to update cluster member entry during shutdown");
                        return;
                    }

                    _log.Warning(f.Cause, "Failed to update TTL heartbeat, will retry on next interval");
                    break;

                default:
                    Unhandled(message);
                    break;
            }
        }

        private async Task<Status> ExecuteUpdateAsync()
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
                cts.CancelAfter(_operationTimeout);

                await _client.UpdateAsync(cts.Token);
                return Status.Success.Instance;
            }
            catch (Exception ex)
            {
                return new Status.Failure(ex);
            }
        }

        /// <summary>
        /// Timer scheduler for periodic heartbeat
        /// </summary>
        public ITimerScheduler? Timers { get; set; }
    }
}
