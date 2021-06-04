// -----------------------------------------------------------------------
// <copyright file="AzureLeaseActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2021 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Util;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using static Akka.Coordination.Azure.AzureLeaseProtocol;

namespace Akka.Coordination.Azure
{
    /// <summary>
    /// INTERNAL API
    /// </summary>
    internal static class AzureLeaseProtocol
    {
        public enum State
        {
            Starting,
            Idle,
            PendingRead,
            Granting,
            Granted,
            Releasing
        }

        public interface IData{}

        public sealed class ReadRequired : IData
        {
            public static readonly ReadRequired Instance = new ReadRequired();
            private ReadRequired(){}
        }

        /// <summary>
        /// Known version from when the lease was cleared with Blob storage.
        ///
        /// A subsequent update can try without reading with the given version
        /// as it was from an update that set client to None
        /// </summary>
        public sealed class LeaseCleared : IData
        {
            public LeaseCleared(string version)
            {
                Version = version;
            }

            public string Version { get; }
        }

        public interface IReplyRequired
        {
            IActorRef ReplyTo { get; }
        }

        /// <summary>
        /// Awaiting a read to try and get the lease.
        /// </summary>
        public sealed class PendingReadData : IReplyRequired
        {
            public PendingReadData(IActorRef replyTo, Action<Exception> leaseLostCallback)
            {
                ReplyTo = replyTo;
                LeaseLostCallback = leaseLostCallback;
            }

            public Action<Exception> LeaseLostCallback { get; }
            public IActorRef ReplyTo { get; }
        }

        public sealed class OperationInProgress : IData, IReplyRequired
        {
            public OperationInProgress(IActorRef replyTo, string version, Action<Exception> leaseLostCallback)
            {
                ReplyTo = replyTo;
                Version = version;
                LeaseLostCallback = leaseLostCallback;
                OperationStartTime = DateTime.UtcNow.Ticks;
            }

            public IActorRef ReplyTo { get; }

            public string Version { get; }
            public Action<Exception> LeaseLostCallback { get; }

            public long OperationStartTime { get; }
        }

        public sealed class GrantedVersion : IData
        {
            public GrantedVersion(string version, Action<Exception> leaseLostCallback)
            {
                Version = version;
                LeaseLostCallback = leaseLostCallback;
            }

            public string Version { get; }

            public Action<Exception> LeaseLostCallback { get; }
        }

        public interface ICommand{}

        /// <summary>
        /// Acquire the <see cref="BlobLeaseClient"/> reference for this lease.
        /// </summary>
        public sealed class Init : ICommand
        {
            public static readonly Init Instance = new Init();
            private Init(){}
        }

        public sealed class Acquire : ICommand
        {
            public Acquire(Action<Exception> leaseLostCallback = null)
            {
                LeaseLostCallback = leaseLostCallback;
            }

            public Action<Exception> LeaseLostCallback { get; }
        }

        #region Internal
        public sealed class Release : ICommand
        {
        }

        public sealed class ReadResponse : ICommand
        {
            public ReadResponse(AzureLeaseResource response)
            {
                Response = response;
            }

            public AzureLeaseResource Response { get; }
        }

        public sealed class Heartbeat : ICommand
        {
            public static readonly Heartbeat Instance = new Heartbeat();
            private Heartbeat(){}
        }

        #endregion

        public interface IResponse{}

        public sealed class LeaseAcquired : IResponse
        {
            public static readonly LeaseAcquired Instance = new LeaseAcquired();
            private LeaseAcquired(){}
        }

        public sealed class LeaseTaken : IResponse
        {
            public static readonly LeaseTaken Instance = new LeaseTaken();
            private LeaseTaken(){}
        }

        public sealed class LeaseReleased : IResponse, IDeadLetterSuppression
        {
            public static readonly LeaseReleased Instance = new LeaseReleased();
            private LeaseReleased(){}
        }

        public sealed class InvalidRequest : IResponse, IDeadLetterSuppression
        {
            public InvalidRequest(string reason)
            {
                Reason = reason;
            }

            public string Reason { get; }
        }
    }

    /// <summary>
    /// INTERNAL API.
    ///
    /// Actor that actually controls the underlying <see cref="AzureLease"/>
    /// </summary>
    internal sealed class AzureLeaseActor : FSM<State, IData>
    {
        private readonly AzureLeaseConfig _azureConfig;
        private readonly LeaseSettings _settings;
        private BlobServiceClient _client;
        private BlobLeaseClient _leaseClient;
        private readonly string _leaseName;
        private AtomicBoolean _granted;
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public AzureLeaseActor(AzureLeaseConfig azureConfig, LeaseSettings settings, string leaseName, AtomicBoolean granted)
        {
            _azureConfig = azureConfig;
            _settings = settings;
            _leaseName = leaseName;
            _granted = granted;

            StartWith(State.Starting, ReadRequired.Instance);
            When(State.Starting, Starting);
            When(State.Idle, Idle);
        }

        private State<State, IData> Starting(Event<IData> fsmevent)
        {
            switch (fsmevent.FsmEvent)
            {
                case Init _:
                    var self = Self;
                    GetLeaseClient(5).PipeTo(self);
                    return Stay();
                case BlobLeaseClient client:
                    _leaseClient = client;
                    return GoTo(State.Idle);
                default:
                    Unhandled(fsmevent);
                    return Stay();
            }
        }

        private State<State, IData> Idle(Event<IData> fsmevent)
        {
            switch (fsmevent.FsmEvent)
            {
                case Acquire a when fsmevent.StateData is ReadRequired:
                    GetLeaseClient()

                default:
                    Unhandled(fsmevent);
                    return Stay();
            }
        }

        protected override void PreStart()
        {
            _log.Debug("Initializing Azure Container Storage...");
            _client = new BlobServiceClient(_azureConfig.ConnectionString);
            Self.Tell(Init.Instance); // acquire the leaseclient
            base.PreStart();
        }

        private static readonly Dictionary<int, TimeSpan> RetryInterval =
            new Dictionary<int, TimeSpan>()
            {
                { 5, TimeSpan.FromMilliseconds(100) },
                { 4, TimeSpan.FromMilliseconds(500) },
                { 3, TimeSpan.FromMilliseconds(1000) },
                { 2, TimeSpan.FromMilliseconds(2000) },
                { 1, TimeSpan.FromMilliseconds(4000) },
                { 0, TimeSpan.FromMilliseconds(8000) },
            };

        private async Task<BlobLeaseClient> GetLeaseClient(int remainingTries)
        {
            var blobContainerClient = await InitCloudStorage(remainingTries);
            var leaseClient = blobContainerClient.GetBlobLeaseClient(_settings.LeaseName);
            
            _log.Debug("Successfully acquired LeaseClientReference to [{0}] - ready to run.", leaseClient.LeaseId);
            return leaseClient;
        }

        private async Task<BlobContainerClient> InitCloudStorage(int remainingTries)
        {
            try
            {
                var blobClient = _client.GetBlobContainerClient(_azureConfig.ContainerName);

                using var cts = new CancellationTokenSource(_azureConfig.ConnectTimeout);
                if (!_azureConfig.AutoInitialize)
                {
                    var exists = await blobClient.ExistsAsync(cts.Token);

                    if (!exists)
                    {
                        remainingTries = 0;

                        throw new Exception(
                            $"Container {_azureConfig.ContainerName} doesn't exist. Either create it or turn auto-initialize on");
                    }

                    _log.Debug("Successfully connected to existing container {0}", _azureConfig.ContainerName);

                    return blobClient;
                }

                if (await blobClient.ExistsAsync(cts.Token))
                {
                    _log.Debug("Successfully connected to existing container {0}", _azureConfig.ContainerName);
                }
                else
                {
                    try
                    {
                        await blobClient.CreateAsync(_azureConfig.ContainerPublicAccessType,
                            cancellationToken: cts.Token);
                        _log.Info("Created Azure Blob Container {0}", _azureConfig.ContainerName);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Failed to create Azure Blob Container {_azureConfig.ContainerName}", e);
                    }
                }

                return blobClient;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[{0}] more tries to initialize table storage remaining...", remainingTries);
                if (remainingTries == 0)
                    throw;
                await Task.Delay(RetryInterval[remainingTries]);
                return await InitCloudStorage(remainingTries - 1);
            }
        }
    }
}