// -----------------------------------------------------------------------
//  <copyright file="RedisDiscoveryGuardian.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Discovery;
using Akka.Event;
using StackExchange.Redis;

namespace Akka.Discovery.Redis.Actors
{
    /// <summary>
    /// Message to stop the discovery service
    /// </summary>
    internal sealed class StopDiscovery
    {
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static readonly StopDiscovery Instance = new StopDiscovery();
        private StopDiscovery() { }
    }

    /// <summary>
    /// Message indicating discovery has stopped
    /// </summary>
    internal sealed class DiscoveryStopped
    {
        /// <summary>
        /// Creates a new DiscoveryStopped message
        /// </summary>
        public DiscoveryStopped(IActorRef replyTo)
        {
            ReplyTo = replyTo;
        }

        /// <summary>
        /// The actor to reply to
        /// </summary>
        public IActorRef ReplyTo { get; }
    }

    /// <summary>
    /// Message indicating discovery stop failed
    /// </summary>
    internal sealed class DiscoveryStopFailed
    {
        /// <summary>
        /// Creates a new DiscoveryStopFailed message
        /// </summary>
        public DiscoveryStopFailed(IActorRef replyTo, Exception cause)
        {
            ReplyTo = replyTo;
            Cause = cause;
        }

        /// <summary>
        /// The actor to reply to
        /// </summary>
        public IActorRef ReplyTo { get; }

        /// <summary>
        /// The exception that caused the failure
        /// </summary>
        public Exception Cause { get; }
    }

    /// <summary>
    /// The guardian actor that owns the Redis connection and manages discovery operations.
    /// Instantiated by <see cref="RedisServiceDiscovery"/> as a system actor. The connection is
    /// established lazily inside the actor lifecycle (never in the service constructor) and all
    /// operations are wrapped in a backoff-retry with a per-operation timeout, so an unreachable
    /// Redis at startup does not fail the ActorSystem and does not cause a busy-retry loop.
    /// The actor honors a single lookup at a time; requests made while one is underway are ignored.
    /// </summary>
    internal sealed class RedisDiscoveryGuardian : UntypedActor
    {
        private sealed class Start
        {
            public static readonly Start Instance = new Start();
            private Start() { }
        }

        /// <summary>
        /// Creates Props for the RedisDiscoveryGuardian actor
        /// </summary>
        public static Props Props(RedisDiscoverySettings settings)
            => global::Akka.Actor.Props.Create(() => new RedisDiscoveryGuardian(settings)).WithDeploy(Deploy.Local);

        private static int _startRetryCount;
        private static readonly Status.Failure DefaultFailure = new Status.Failure(null);

        private readonly ILoggingAdapter _log;
        private readonly RedisDiscoverySettings _settings;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _backoff;
        private readonly TimeSpan _maxBackoff;
        private readonly TimeSpan _staleTtlThreshold;
        private readonly bool _readOnly;
        private readonly CancellationTokenSource _shutdownCts;

        private IConnectionMultiplexer? _connection;
        private ClusterMemberRedisClient? _client;
        private int _retryCount;
        private bool _lookingUp;
        private IActorRef? _requester;

        /// <summary>
        /// Creates a new RedisDiscoveryGuardian actor
        /// </summary>
        public RedisDiscoveryGuardian(RedisDiscoverySettings settings)
        {
            _settings = settings;
            _timeout = settings.OperationTimeout;
            _backoff = settings.RetryBackoff;
            _maxBackoff = settings.MaximumRetryBackoff;
            _staleTtlThreshold = settings.EffectiveStaleTtlThreshold;
            _readOnly = settings.ReadOnly;
            _log = Logging.GetLogger(Context.System, nameof(RedisDiscoveryGuardian));
            _shutdownCts = new CancellationTokenSource();
        }

        /// <inheritdoc/>
        protected override void PreStart()
        {
            if (_log.IsDebugEnabled)
                _log.Debug("Actor started");

            base.PreStart();
            Become(Initializing);

            // Perform an actor-start backoff so that a fleet of nodes racing to connect to a
            // recovering Redis does not thundering-herd it.
            var backoff = Clamp(new TimeSpan(_backoff.Ticks * _startRetryCount++));
            if (backoff > TimeSpan.Zero)
                Task.Delay(backoff, _shutdownCts.Token).PipeTo(Self, success: () => Start.Instance);
            else
                Self.Tell(Start.Instance);
        }

        /// <inheritdoc/>
        protected override void PostStop()
        {
            base.PostStop();
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();

            // The guardian owns the connection lifecycle, so it is disposed here regardless of how
            // the actor stopped (clean shutdown or PoisonPill).
            _connection?.Dispose();

            if (_log.IsDebugEnabled)
                _log.Debug("Actor stopped");
        }

        private bool Initializing(object message)
        {
            switch (message)
            {
                case Start _ when !_readOnly:
                    _retryCount = 0;
                    ExecuteOperationWithRetry(async token =>
                    {
                        await EnsureClientAsync(token);
                        return await _client!.GetOrCreateAsync(token);
                    }).PipeTo(Self);
                    return true;

                case Start _:
                    // Read-only mode does not register a self entry; the connection is established
                    // lazily on the first lookup instead.
                    Become(Running);

                    if (_log.IsDebugEnabled)
                        _log.Debug("Actor initialized (read-only)");
                    return true;

                case Status.Success _:
                    _startRetryCount = 0;
                    Context.ActorOf(HeartbeatActor.Props(_settings, _client!));
                    Become(Running);

                    if (_log.IsDebugEnabled)
                        _log.Debug("Actor initialized");
                    return true;

                case Status.Failure f:
                    _log.Warning(f.Cause, "Failed to create/retrieve self discovery entry, retrying.");

                    ExecuteOperationWithRetry(async token =>
                    {
                        await EnsureClientAsync(token);
                        return await _client!.GetOrCreateAsync(token);
                    }).PipeTo(Self);
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
                        if (_log.IsDebugEnabled)
                            _log.Debug("Another lookup operation is still underway, ignoring request.");
                        return true;
                    }

                    if (lookup.ServiceName != _settings.ServiceName)
                    {
                        _log.Error($"Lookup ServiceName mismatch. Expected: {_settings.ServiceName}, received: {lookup.ServiceName}");
                        Sender.Tell(ImmutableList<ClusterMember>.Empty);
                        return true;
                    }

                    _lookingUp = true;
                    _retryCount = 0;
                    _requester = Sender;
                    if (_log.IsDebugEnabled)
                        _log.Debug("Lookup started for service {0}, stale TTL threshold: {1}", lookup.ServiceName, _staleTtlThreshold);

                    ExecuteOperationWithRetry(async token =>
                    {
                        await EnsureClientAsync(token);
                        return await _client!.GetAllAsync(_staleTtlThreshold, token);
                    }).PipeTo(Self);
                    return true;

                case Status.Success result:
                    _requester?.Tell(result.Status);
                    _lookingUp = false;
                    return true;

                case Status.Failure fail:
                    _log.Warning(fail.Cause, "Failed to execute discovery lookup, retrying.");
                    ExecuteOperationWithRetry(async token =>
                    {
                        await EnsureClientAsync(token);
                        return await _client!.GetAllAsync(_staleTtlThreshold, token);
                    }).PipeTo(Self);
                    return true;

                case StopDiscovery _:
                    foreach (var child in Context.GetChildren())
                        Context.Stop(child);

                    if (!_readOnly && _client is { })
                    {
                        var sender = Sender;
                        RemoveSelfAsync()
                            .PipeTo(Self,
                                success: () => new DiscoveryStopped(sender),
                                failure: e => new DiscoveryStopFailed(sender, e));
                        Become(Stopping);
                    }
                    else
                    {
                        Sender.Tell(global::Akka.Done.Instance);
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
                    Sender.Tell(global::Akka.Done.Instance);
                    return true;

                case DiscoveryStopped msg:
                    msg.ReplyTo.Tell(global::Akka.Done.Instance);
                    Context.System.Stop(Self);
                    return true;

                case DiscoveryStopFailed fail:
                    _log.Warning(fail.Cause, "Failed to perform cleanup, node entry has not been removed from storage");
                    fail.ReplyTo.Tell(global::Akka.Done.Instance);
                    Context.System.Stop(Self);
                    return true;

                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        protected override void OnReceive(object message)
        {
            throw new NotImplementedException("Should never reach this code");
        }

        private async Task EnsureClientAsync(CancellationToken token)
        {
            if (_client != null)
                return;

            // AbortOnConnectFail=false lets the multiplexer connect even when Redis is momentarily
            // unavailable and keep reconnecting in the background; transient operation failures are
            // handled by ExecuteOperationWithRetry rather than at connect time.
            var options = ConfigurationOptions.Parse(_settings.ConnectionString);
            options.AbortOnConnectFail = false;

            _connection = await ConnectionMultiplexer.ConnectAsync(options);
            _client = new ClusterMemberRedisClient(_connection, _settings, _log);
        }

        private async Task RemoveSelfAsync()
        {
            await _client!.RemoveSelfAsync(_shutdownCts.Token);
        }

        // Always call this method using PipeTo; we wait for Status.Success or Status.Failure asynchronously.
        private async Task<Status> ExecuteOperationWithRetry<T>(Func<CancellationToken, Task<T>> operation)
        {
            var backoff = Clamp(new TimeSpan(_backoff.Ticks * _retryCount++));
            if (backoff > TimeSpan.Zero)
                await Task.Delay(backoff, _shutdownCts.Token);

            if (_shutdownCts.IsCancellationRequested)
                return DefaultFailure;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCts.Token);
            cts.CancelAfter(_timeout);

            // Any exception thrown from the async method is converted to Status.Failure by PipeTo.
            var result = await operation(cts.Token);
            return new Status.Success(result);
        }

        private TimeSpan Clamp(TimeSpan backoff)
            => backoff > _maxBackoff ? _maxBackoff : backoff;
    }
}
