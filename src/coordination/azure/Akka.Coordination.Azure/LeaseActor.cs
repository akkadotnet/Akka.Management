//-----------------------------------------------------------------------
// <copyright file="LeaseActor.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Util;
using Azure;

#nullable enable
namespace Akka.Coordination.Azure
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    internal sealed class LeaseActor : FSM<LeaseActor.IState, LeaseActor.IData>, IWithTimers
    {
        #region Statics
        public interface IState { }
        
        public sealed class Idle: IState
        {
            public static readonly Idle Instance = new Idle();
            private Idle() { }
        }
        
        public sealed class PendingRead: IState
        {
            public static readonly PendingRead Instance = new PendingRead();
            private PendingRead() { }
        }
        
        public sealed class Granting: IState
        {
            public static readonly Granting Instance = new Granting();
            private Granting() { }
        }
        
        public sealed class Granted: IState
        {
            public static readonly Granted Instance = new Granted();
            private Granted() { }
        }
        
        public sealed class Releasing: IState
        {
            public static readonly Releasing Instance = new Releasing();
            private Releasing() { }
        }
        
        public interface IData{}
        public sealed class ReadRequired: IData
        {
            public static readonly ReadRequired Instance = new ReadRequired();
            private ReadRequired() { }
        }
        
        // Known version from when the lease was cleared. A subsequent update can try without reading
        // with the given version as it was from an update that set client to None
        public sealed class LeaseCleared: IData
        {
            public ETag Version { get; }
            public LeaseCleared(ETag version)
            {
                Version = version;
            }
        }
        
        public interface IReplyRequired : IData
        {
            IActorRef ReplyTo { get; }
        }
        
        // Awaiting a read to try and get the lease
        public sealed class PendingReadData: IReplyRequired
        {
            public PendingReadData(IActorRef replyTo, Action<Exception?> leaseLostCallback)
            {
                ReplyTo = replyTo;
                LeaseLostCallback = leaseLostCallback;
            }

            public IActorRef ReplyTo { get; }
            public Action<Exception?> LeaseLostCallback { get; }
        }
        
        public sealed class OperationInProgress: IReplyRequired
        {
            public OperationInProgress(
                IActorRef replyTo,
                ETag version,
                Action<Exception?> leaseLostCallback,
                DateTime? operationStartTime = null)
            {
                ReplyTo = replyTo;
                Version = version;
                LeaseLostCallback = leaseLostCallback;
                OperationStartTime = operationStartTime ?? DateTime.UtcNow;
            }

            public IActorRef ReplyTo { get; }
            public ETag Version { get; }
            public Action<Exception?> LeaseLostCallback { get; }
            public DateTime OperationStartTime { get; }
        }
        
        public sealed class GrantedVersion: IData
        {
            public GrantedVersion(ETag version, Action<Exception?> leaseLostCallback)
            {
                Version = version;
                LeaseLostCallback = leaseLostCallback;
            }

            public ETag Version { get; }
            public Action<Exception?> LeaseLostCallback { get; }

            public GrantedVersion Copy(ETag? version = null, Action<Exception?>? leaseLostCallback = null)
                => new GrantedVersion(
                    version: version ?? Version,
                    leaseLostCallback: leaseLostCallback ?? LeaseLostCallback);
        }
        
        public interface ICommand { }
        public sealed class Acquire: ICommand
        {
            public Acquire(Action<Exception?>? leaseLostCallback = null)
            {
                LeaseLostCallback = leaseLostCallback ?? EmptyAction;
            }

            public Action<Exception?> LeaseLostCallback { get; }
            
            private static void EmptyAction(Exception? e) { }
        }
        public sealed class Release: ICommand
        {
            public static readonly Release Instance = new Release();
            private Release() { }
        }
        
        // Internal
        public sealed class ReadResponse: ICommand
        {
            public ReadResponse(LeaseResource response)
            {
                Response = response;
            }

            public LeaseResource Response { get; }
        }
        
        public sealed class WriteResponse : ICommand
        {
            public WriteResponse(Either<LeaseResource, LeaseResource> response)
            {
                Response = response;
            }

            public Either<LeaseResource, LeaseResource> Response { get; }
        }
        
        public sealed class Heartbeat: ICommand
        {
            public static readonly Heartbeat Instance = new Heartbeat();
            private Heartbeat() { }
        }
        
        public interface IResponse {}
        public sealed class LeaseAcquired: IResponse
        {
            public static readonly LeaseAcquired Instance = new LeaseAcquired();
            private LeaseAcquired() { }
        }

        public sealed class LeaseTaken: IResponse
        {
            public static readonly LeaseTaken Instance = new LeaseTaken();
            private LeaseTaken() {}
        }
        
        public sealed class LeaseReleased: IResponse, IDeadLetterSuppression
        {
            public static readonly LeaseReleased Instance = new LeaseReleased();
            private LeaseReleased() {}
        }
        
        public sealed class InvalidReleaseRequest: IResponse, IDeadLetterSuppression
        {
            public static readonly InvalidReleaseRequest Instance = new ();
            private InvalidReleaseRequest()
            { }
        }

        public static Props Props(IAzureApi client, LeaseSettings leaseSettings, string leaseName, AtomicBoolean granted)
            => Actor.Props.Create(() => new LeaseActor(client, leaseSettings, leaseName, granted)); 
        #endregion

        private readonly string _leaseName;
        private readonly string _ownerName;
        private readonly IAzureApi _client;
        private readonly ILoggingAdapter _log;

        private readonly TimeSpan _timeoutOffset;
        
#pragma warning disable 8618
        public LeaseActor(IAzureApi client, LeaseSettings settings, string leaseName, AtomicBoolean granted)
#pragma warning restore 8618
        {
            _client = client;
            _leaseName = leaseName;
            _timeoutOffset = settings.TimeoutSettings.HeartbeatTimeout - 
                             new TimeSpan(2 * settings.TimeoutSettings.HeartbeatInterval.Ticks);
            
            var localGranted = granted;

            _log = Context.GetLogger();
            _ownerName = settings.OwnerName;
            
            StartWith(Idle.Instance, ReadRequired.Instance);
            
            When(Idle.Instance, evt =>
            {
                if (!(evt.FsmEvent is Acquire acquire))
                {
                    if(_log.IsDebugEnabled)
                        _log.Debug($"[Idle] Received event is not Acquire. Received: [{evt.FsmEvent.GetType()}]");
                    return null;
                }
                
                switch (evt.StateData)
                {
                    case ReadRequired _:
                        if(_log.IsDebugEnabled)
                            _log.Debug("[Idle] Received Acquire, ReadRequired.");
                        _client.ReadOrCreateLeaseResource(leaseName)
                            .ContinueWith(task => new ReadResponse(task.Result))
                            .PipeTo(Self, failure: FlattenAggregateException);
                        return GoTo(PendingRead.Instance)
                            .Using(new PendingReadData(Sender, acquire.LeaseLostCallback));
                    case LeaseCleared cleared:
                        if(_log.IsDebugEnabled)
                            _log.Debug("[Idle] Received Acquire, LeaseCleared.");
                        _client.UpdateLeaseResource(leaseName, _ownerName, cleared.Version)
                            .ContinueWith(task => new WriteResponse(task.Result))
                            .PipeTo(Self, failure: FlattenAggregateException);
                        return GoTo(Granting.Instance)
                            .Using(new OperationInProgress(Sender, cleared.Version, acquire.LeaseLostCallback));
                    default:
                        return null;
                }
            });
            
            When(PendingRead.Instance, @event =>
            {
                if(!(@event.FsmEvent is ReadResponse evt))
                {
                    if(_log.IsDebugEnabled)
                        _log.Debug($"[PendingRead] Received event is not ReadResponse. Received: [{@event.FsmEvent.GetType()}]");
                    return null;
                }
                
                var resource = evt.Response;
                var data = (PendingReadData) @event.StateData;
                var who = data.ReplyTo;
                var leaseLost = data.LeaseLostCallback;
                var version = resource.Version;

                // Lock not taken
                if (resource.Owner == null)
                {
                    if(_log.IsDebugEnabled)
                        _log.Debug("[PendingRead] Lease has not been taken, trying to get lease.");
                    return TryGetLease(version, who, leaseLost);
                }

                var currentOwner = resource.Owner;
                var time = resource.Time;
                if (currentOwner.Equals(_ownerName))
                {
                    if (HasLeaseTimedOut(time))
                    {
                        // We have the lock from a different incarnation
                        _log.Warning(
                            "Lease {0} requested by client {1} is already owned by client. Previous lease was not " +
                            "released due to ungraceful shutdown. Lease time {2} is close or past expiry so re-acquiring",
                            leaseName,
                            _ownerName,
                            time);
                        return TryGetLease(version, who, leaseLost);
                    }

                    _log.Warning(
                        "Lease {0} requested by client {1} is already owned by client. Previous lease was not released due to ungraceful shutdown. " +
                        "Lease is still within timeout so granting immediately",
                        leaseName,
                        _ownerName);
                    who.Tell(LeaseAcquired.Instance);
                    return GoTo(Granted.Instance).Using(new GrantedVersion(version, leaseLost));
                }
                
                if (HasLeaseTimedOut(time))
                {
                    _log.Warning(
                        "Lease {0} has reached TTL. Owner {1} has failed to heartbeat, have they crashed?. Allowing {2} to try and take lease",
                        leaseName,
                        currentOwner,
                        _ownerName);
                    return TryGetLease(version, who, leaseLost);
                }
                who.Tell(LeaseTaken.Instance);
                // Even though we have a version there is no benefit to storing it as we can't update a lease that has a client
                return GoTo(Idle.Instance).Using(ReadRequired.Instance);
            });
            
            When(Granting.Instance, @event =>
            {
                if (!(@event.FsmEvent is WriteResponse writeResponse))
                {
                    if(_log.IsDebugEnabled)
                        _log.Debug($"[Granting] Received event is not WriteResponse. Received: [{@event.FsmEvent.GetType()}]");
                    return null;
                }
                
                var evt = writeResponse.Response;
                var data = (OperationInProgress) @event.StateData;
                var who = data.ReplyTo;
                var oldVersion = data.Version;
                var leaseLost = data.LeaseLostCallback;
                var operationStartTime = data.OperationStartTime;

                if (evt is Right<LeaseResource, LeaseResource> response)
                {
                    if (oldVersion == response.Value.Version)
                        throw new LeaseException(
                            $"Requirement failed: Update response from Azure Blob should not return the same version: Response: {response.Value}. Client: {data}");
                    var operationDuration = DateTime.UtcNow - operationStartTime;
                    if (operationDuration > new TimeSpan(settings.TimeoutSettings.HeartbeatTimeout.Ticks / 2))
                    {
                        _log.Warning("API server took too long to respond to update: {0} ms. ",
                            operationDuration.TotalMilliseconds);
                        who.Tell(new Status.Failure(new LeaseTimeoutException($"API server took too long to respond: {operationDuration.TotalMilliseconds}")));
                        return GoTo(Idle.Instance).Using(ReadRequired.Instance);
                    }

                    localGranted.GetAndSet(true);
                    who.Tell(LeaseAcquired.Instance);
                    return GoTo(Granted.Instance).Using(new GrantedVersion(response.Value.Version, leaseLost));
                }

                var leftResponse = ((Left<LeaseResource, LeaseResource>) evt).Value;
                var version = leftResponse.Version;
                if (leftResponse.Owner is null)
                {
                    if (oldVersion == leftResponse.Version)
                        throw new LeaseException(
                            $"Update response from Azure Blob should not return the same version: Response: {leftResponse}. Client: {data}");
                    // Try again as lock version has moved on but is not taken
                    who.Tell(LeaseAcquired.Instance);
                    _client.UpdateLeaseResource(leaseName, _ownerName, version)
                        .ContinueWith(t => new WriteResponse(t.Result))
                        .PipeTo(Self, failure: FlattenAggregateException);
                    return Stay();
                }
                // The audacity, someone else has taken the lease :(
                who.Tell(LeaseTaken.Instance);
                return GoTo(Idle.Instance).Using(ReadRequired.Instance);
            });

            When(Granted.Instance, evt =>
            {
                if (!(evt.StateData is GrantedVersion gv))
                    return null;
                
                var version = gv.Version;
                var leaseLost = gv.LeaseLostCallback;

                switch (evt.FsmEvent)
                {
                    case Heartbeat _:
                        _log.Debug("Heartbeat: updating lease time. Version {0}", version);
                        _client.UpdateLeaseResource(leaseName, _ownerName, version)
                            .ContinueWith(t => new WriteResponse(t.Result))
                            .PipeTo(Self, failure: FlattenAggregateException);
                        return Stay();
                    
                    case WriteResponse {Response: Right<LeaseResource, LeaseResource> resource}:
                        if (!resource.Value.Owner?.Contains(_ownerName) ?? false)
                            throw new LeaseException($"response from API server has different owner for success: {resource}");
                        _log.Debug("Heartbeat: lease time updated: Version {0}", resource.Value.Version);
                        Timers!.StartSingleTimer("heartbeat", Heartbeat.Instance, settings.TimeoutSettings.HeartbeatInterval);
                        return Stay().Using(gv.Copy(version: resource.Value.Version));
                    
                    case WriteResponse {Response: Left<LeaseResource, LeaseResource> resource}:
                        _log.Warning("Conflict during heartbeat to lease {0}. Lease assumed to be released.", resource.Value);
                        localGranted.GetAndSet(false);
                        ExecuteLeaseLockCallback(leaseLost, null);
                        return GoTo(Idle.Instance).Using(ReadRequired.Instance);
                    
                    case Status.Failure failure:
                        // FIXME, retry if timeout far enough off: https://github.com/lightbend/akka-commercial-addons/issues/501
                        _log.Warning(failure.Cause, "Failure during heartbeat to lease. Lease assumed to be released.");
                        localGranted.GetAndSet(false);
                        ExecuteLeaseLockCallback(leaseLost, failure.Cause);
                        return GoTo(Idle.Instance).Using(ReadRequired.Instance);
                        
                    case Release _:
                        _client.UpdateLeaseResource(leaseName, "", version)
                            .ContinueWith(t => new WriteResponse(t.Result))
                            .PipeTo(Self, failure: FlattenAggregateException);
                        return GoTo(Releasing.Instance).Using(new OperationInProgress(Sender, version, leaseLost));
                    
                    case Acquire acquire:
                        Sender.Tell(LeaseAcquired.Instance);
                        return Stay().Using(gv.Copy(leaseLostCallback: acquire.LeaseLostCallback));
                    
                    default:
                        return null;
                }
            });
            
            When(Releasing.Instance, @event =>
            {
                if (!(@event.FsmEvent is WriteResponse writeResponse))
                    return null;
                
                // FIXME deal with failure from releasing the the lock, currently handled in whenUnhandled but could retry to remove: https://github.com/lightbend/akka-commercial-addons/issues/502
                var response = writeResponse.Response;
                var data = (OperationInProgress) @event.StateData;
                var who = data.ReplyTo;

                if (response is Right<LeaseResource, LeaseResource> right)
                {
                    var lr = right.Value;
                    if(!string.IsNullOrWhiteSpace(lr.Owner))
                        throw new LeaseException(
                            $"Requirement failed: Released lease has unexpected owner: {lr}");
                    who.Tell(LeaseReleased.Instance);
                    return GoTo(Idle.Instance).Using(new LeaseCleared(lr.Version));
                }

                var left = ((Left<LeaseResource, LeaseResource>) response).Value;
                if (left.Owner is null)
                {
                    _log.Warning(
                        "Release conflict and owner has been removed: {0}. Lease will continue to work but TTL must have been reached to allow another node to remove lease.",
                        left);
                    who.Tell(LeaseReleased.Instance);
                    return GoTo(Idle.Instance).Using(ReadRequired.Instance);
                }
                
                _log.Warning(
                    "Release conflict and owner has changed: {0}. Lease will continue to work but TTL must have been reached to allow another node to remove lease.",
                    left);
                who.Tell(LeaseReleased.Instance);
                return GoTo(Idle.Instance).Using(ReadRequired.Instance);
            });
            
            WhenUnhandled(@event =>
            {
                switch (@event.FsmEvent)
                {
                    case Acquire _:
                        _log.Info(
                            "Acquire request for owner {0} lease {1} while previous acquire/release still in progress. Current state: {2}",
                            _ownerName,
                            leaseName,
                            StateName);
                        return Stay().Using(@event.StateData);
                    
                    case Release _:
                        _log.Info(
                            "Release request for owner {0} lease {1} while previous acquire/release still in progress. Current state: {2}",
                            _ownerName,
                            leaseName,
                            StateName);
                        Sender.Tell(InvalidReleaseRequest.Instance);
                        return Stay().Using(@event.StateData);
                    
                    case Status.Failure f when @event.StateData is IReplyRequired replyRequired:
                        _log.Warning(
                            f.Cause,
                            "Failure communicating with the API server for owner {0} lease {1}: [{2}]. Current state: {3}",
                            _ownerName,
                            leaseName,
                            f.Cause.Message,
                            StateName);
                        replyRequired.ReplyTo.Tell(new Status.Failure(f.Cause));
                        return GoTo(Idle.Instance).Using(ReadRequired.Instance);
                    
                    default:
                        return null;
                }
            });
            
            OnTransition( (from, to) =>
            {
                if (to is Granted)
                {
                    Timers!.StartSingleTimer("heartbeat", Heartbeat.Instance, settings.TimeoutSettings.HeartbeatInterval);
                } 
                else if (from is Granted)
                {
                    Timers!.Cancel("heartbeat");
                    localGranted.GetAndSet(false);
                }
            });
            
            Initialize();
        }

        protected override void PreStart()
        {
            if(_log.IsDebugEnabled)
                _log.Debug("LeaseActor started");
            base.PreStart();
        }

        private static Status.Failure FlattenAggregateException(Exception e)
        {
            if (!(e is AggregateException agg)) 
                return new Status.Failure(e);
            
            agg = agg.Flatten();
            return agg.InnerExceptions.Count == 1 
                ? new Status.Failure(agg.InnerExceptions.First()) 
                : new Status.Failure(agg);
        }

        private void ExecuteLeaseLockCallback(Action<Exception?> callback, Exception? result)
        {
            try
            {
                callback(result);
            }
            catch (Exception e)
            {
                _log.Warning(e, "Lease lost callback threw exception");
                throw;
            }
        }

        private State<IState, IData> TryGetLease(ETag version, IActorRef reply, Action<Exception?> leaseLost)
        {
            _client.UpdateLeaseResource(_leaseName, _ownerName, version)
                .ContinueWith(t => new WriteResponse(t.Result))
                .PipeTo(Self, failure: FlattenAggregateException);
            return GoTo(Granting.Instance).Using(new OperationInProgress(reply, version, leaseLost));
        }

        private bool HasLeaseTimedOut(DateTimeOffset leaseTime)
            => DateTimeOffset.UtcNow > leaseTime + _timeoutOffset;
        
        public ITimerScheduler Timers { get; set; } 
    }
}