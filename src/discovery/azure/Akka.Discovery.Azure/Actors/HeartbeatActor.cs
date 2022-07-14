// -----------------------------------------------------------------------
//  <copyright file="HeartbeatActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using Akka.Actor;
using Akka.Event;

namespace Akka.Discovery.Azure.Actors
{
    internal sealed class HeartbeatActor: UntypedActor, IWithTimers
    {
        public static Props Props(AzureDiscoverySettings settings, ClusterMemberTableClient client)
            => Actor.Props.Create(() => new HeartbeatActor(settings, client)).WithDeploy(Deploy.Local);

        private readonly string _heartbeatTimerKey = "heartbeat-key";
        private readonly string _heartbeat = "heartbeat";
        private readonly ILoggingAdapter _log;
        private readonly ClusterMemberTableClient _client;
        private readonly TimeSpan _heartbeatInterval;
        private readonly CancellationTokenSource _shutdownCts;

        private bool _updating;
        
        public HeartbeatActor(AzureDiscoverySettings settings, ClusterMemberTableClient client)
        {
            _client = client;
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
                    if (_updating)
                        break;
                    
                    _updating = true;
                    if(_log.IsDebugEnabled)
                        _log.Debug("Updating cluster member entry TTL");
                    
                    _client.UpdateAsync(_shutdownCts.Token)
                        .PipeTo(Self, success: _ => Done.Instance);
                    break;
                
                case Done _:
                    _updating = false;
                    break;
                
                case Status.Failure f:
                    _updating = false;
                    _log.Warning(f.Cause, "Failed to update TTL heartbeat");
                    break;
                
                default:
                    Unhandled(message);
                    break;
            }
        }

        public ITimerScheduler Timers { get; set; }
    }
}