// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoveryGuardian.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Discovery.Azure.Model;
using Akka.Event;
using Akka.Util.Internal;

namespace Akka.Discovery.Azure.Actors
{
    internal sealed class StopDiscovery
    {
        public static readonly StopDiscovery Instance = new StopDiscovery();
        private StopDiscovery()
        { }
    }
    
    internal sealed class DiscoveryStopped
    {
        public DiscoveryStopped(IActorRef replyTo)
        {
            ReplyTo = replyTo;
        }

        public IActorRef ReplyTo { get; }
    }

    internal sealed class DiscoveryStopFailed
    {
        public DiscoveryStopFailed(IActorRef replyTo, Exception cause)
        {
            ReplyTo = replyTo;
            Cause = cause;
        }

        public IActorRef ReplyTo { get; }
        public Exception Cause { get; }
    }
    
    /// <summary>
    /// The guardian actor that manages the Azure client instance and the table entries management actors.
    /// Instantiated by AzureServiceDiscovery as a system actor and should restart itself on failures.
    /// The actor will only honor a single lookup request at a time, any requests done while it is still processing
    /// a lookup is ignored.
    /// The actor will reply with an empty result if it is still initializing.
    /// </summary>
    internal sealed class AzureDiscoveryGuardian: UntypedActor
    {
        private sealed class Start
        {
            public static readonly Start Instance = new Start();
            private Start()
            { }
        }
        
        public static Props Props(AzureDiscoverySettings settings)
            => Actor.Props.Create(() => new AzureDiscoveryGuardian(settings)).WithDeploy(Deploy.Local);

        private static int _startRetryCount;
        private static readonly Status.Failure DefaultFailure = new Status.Failure(null);
        
        private readonly ILoggingAdapter _log;
        private readonly AzureDiscoverySettings _settings;
        private ClusterMemberTableClient? _clientDoNotUseDirectly;
        private readonly TimeSpan _staleTtlThreshold;
        private readonly TimeSpan _timeout;
        private string? _host;
        private IPAddress? _address;
        private readonly int _port;
        private readonly CancellationTokenSource _shutdownCts;

        private readonly bool _readOnly;
        private readonly TimeSpan _backoff;
        private readonly TimeSpan _maxBackoff;
        private int _retryCount;
        private bool _lookingUp;
        private IActorRef? _requester;

        private ClusterMemberTableClient Client
        {
            get
            {
                if(_clientDoNotUseDirectly != null)
                    return _clientDoNotUseDirectly;
                
                _clientDoNotUseDirectly = new ClusterMemberTableClient(_settings, _log);
                
                return _clientDoNotUseDirectly;
            }
        }

        public AzureDiscoveryGuardian(AzureDiscoverySettings settings)
        {
            _settings = settings;
            _timeout = settings.OperationTimeout;
            _backoff = settings.RetryBackoff;
            _maxBackoff = settings.MaximumRetryBackoff;
            _log = Logging.GetLogger(Context.System, nameof(AzureDiscoveryGuardian));
            _staleTtlThreshold = settings.EffectiveStaleTtlThreshold;
            
            _port = settings.Port;
            _shutdownCts = new CancellationTokenSource();
            _readOnly = settings.ReadOnly;
        }

        protected override void PreStart()
        {
            if(_log.IsDebugEnabled)
                _log.Debug("Actor started");
            
            base.PreStart();
            Become(Initializing);

            // Do an actor start backoff retry
            // Calculate backoff
            var backoff = new TimeSpan(_backoff.Ticks * _startRetryCount++);
            // Clamp to maximum backoff time
            backoff = backoff.Min(_maxBackoff);
            
            // Perform backoff delay
            if (backoff > TimeSpan.Zero)
                Task.Delay(backoff, _shutdownCts.Token).PipeTo(Self, success: () => Start.Instance);
            else
                Self.Tell(Start.Instance);
        }

        protected override void PostStop()
        {
            base.PostStop();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
            
            if(_log.IsDebugEnabled)
                _log.Debug("Actor stopped");
        }

        private bool Initializing(object message)
        {
            switch (message)
            {
                case Start _ when !_readOnly:
                    if (IPAddress.TryParse(_settings.HostName, out _address))
                    {
                        if (_address.Equals(IPAddress.Any) || _address.Equals(IPAddress.IPv6Any))
                            throw new ConfigurationException($"IPAddress.Any or IPAddress.IPv6Any cannot be used as host address. Was: {_settings.HostName}");

                        _host = null;
                    }
                    else
                    {
                        _host = _settings.HostName;
                        _address = null;
                    }
                    
                    _retryCount = 0;
                    ExecuteOperationWithRetry(async token => 
                            await Client.GetOrCreateAsync(_host, _address, _port, token))
                        .PipeTo(Self);
                    return true;
                
                case Start _:
                    Become(Running);
                    return true;
                
                case Status.Success _:
                    _startRetryCount = 0;
                    Context.ActorOf(HeartbeatActor.Props(_settings, Client));
                    Context.ActorOf(PruneActor.Props(_settings, Client));
                
                    Become(Running);
                
                    if(_log.IsDebugEnabled)
                        _log.Debug("Actor initialized");
                    return true;
                
                case Status.Failure f:
                    if(_log.IsDebugEnabled)
                        _log.Debug(f.Cause, "Failed to create/retrieve self discovery entry, retrying.");
                    
                    ExecuteOperationWithRetry(async token => 
                            await Client.GetOrCreateAsync(_host, _address, _port, token))
                        .PipeTo(Self);
                    return true;
                
                case Lookup _:
                    Sender.Tell(ImmutableList<ClusterMember>.Empty);
                    return true;
                
                default:
                    return false;
            }
        }

        private bool Running(object message)
        {
            switch (message)
            {
                case Lookup lookup:
                    if (_lookingUp)
                    {
                        if(_log.IsDebugEnabled)
                            _log.Debug("Another lookup operation is still underway, ignoring request.");
                        return true;
                    }
                    
                    if (lookup.ServiceName != _settings.ServiceName)
                    {
                        _log.Error(
                            $"Lookup ServiceName mismatch. Expected: {_settings.ServiceName}, received: {lookup.ServiceName}");
                        return true;
                    }
                    
                    _lookingUp = true;
                    _retryCount = 0;
                    _requester = Sender;
                    if(_log.IsDebugEnabled)
                        _log.Debug("Lookup started for service {0}, stale TTL threshold: {1}", lookup.ServiceName, _staleTtlThreshold);

                    ExecuteOperationWithRetry(async token =>
                        await Client.GetAllAsync(
                            lastUpdate: (DateTime.UtcNow - _staleTtlThreshold).Ticks, 
                            token: token))
                        .PipeTo(Self);
                    return true;
                
                case Status.Success result:
                    _requester.Tell(result.Status);
                    _lookingUp = false;
                    return true;
                
                case Status.Failure fail:
                    _log.Warning(fail.Cause, "Failed to execute discovery lookup, retrying.");
                    
                    ExecuteOperationWithRetry(async token =>
                            await Client.GetAllAsync(
                                lastUpdate: (DateTime.UtcNow - _staleTtlThreshold).Ticks, 
                                token: token))
                        .PipeTo(Self);
                    return true;
                
                case StopDiscovery _:
                    foreach (var child in Context.GetChildren())
                        Context.Stop(child);

                    if (!_readOnly)
                    {
                        var sender = Sender;
                        Client.RemoveSelf(_shutdownCts.Token)
                            .PipeTo(Self, 
                                success: () => new DiscoveryStopped(sender), 
                                failure: e => new DiscoveryStopFailed(sender, e));
                        Become(Stopping);
                    }
                    else
                    {
                        Sender.Tell(Done.Instance);
                        Context.Stop(Self);
                    }
                    return true;
                
                default:
                    return false;
            }
        }

        private bool Stopping(object message)
        {
            switch (message)
            {
                case Lookup _:
                    // Ignore lookup messages, we're shutting down
                    Sender.Tell(ImmutableList<ClusterMember>.Empty);
                    return true;
                
                case StopDiscovery _:
                    // Ignore multiple stop messages
                    Sender.Tell(Done.Instance);
                    return true;

                case DiscoveryStopped msg:
                    msg.ReplyTo.Tell(Done.Instance);
                    Context.System.Stop(Self);
                    return true;
                
                case DiscoveryStopFailed fail:
                    _log.Warning(fail.Cause, "Failed to perform cleanup, node entry has not been removed from storage");
                    fail.ReplyTo.Tell(Done.Instance);
                    Context.System.Stop(Self);
                    return true;
                
                default:
                    return false;
            }
        }
        
        protected override void OnReceive(object message)
        {
            throw new NotImplementedException("Should never reach this code");
        }

        // Always call this method using PipeTo, we'll be waiting for Status.Success or Status.Failure asynchronously
        private async Task<Status> ExecuteOperationWithRetry<T>(Func<CancellationToken, Task<T>> operation)
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

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
            cts.CancelAfter(_timeout);
            // Any exception thrown from the async method will be converted to Status.Failure by PipeTo
            var result = await operation(cts.Token);
            return new Status.Success(result);
        }
    }
}