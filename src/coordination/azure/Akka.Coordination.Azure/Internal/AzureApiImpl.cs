// -----------------------------------------------------------------------
//  <copyright file="AzureApiImpl.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Event;
using Akka.Util;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;

namespace Akka.Coordination.Azure.Internal
{
    /// <summary>
    /// Thrown when Azure Blob container initialization fails.
    /// Used to distinguish container-level errors from blob-level <see cref="RequestFailedException"/>
    /// so that callers can retry appropriately.
    /// </summary>
    internal sealed class ContainerInitializationException : Exception
    {
        public int StatusCode { get; }
        public string? ErrorCode { get; }

        public ContainerInitializationException(string message, int statusCode, string? errorCode, Exception innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }

    internal sealed class AzureApiImpl: IAzureApi
    {
        private readonly AzureLeaseSettings _settings;
        private readonly ILoggingAdapter _log;
        private bool _initialized;

        public AzureApiImpl(ActorSystem system, AzureLeaseSettings settings)
        {
            _settings = settings;
            _log = Logging.GetLogger(system, GetType());
        }
        
        [InternalApi]
        public async Task<LeaseResource> ReadOrCreateLeaseResource(string name)
        {
            // TODO: backoff retry
            const int maxTries = 5;
            var tries = 0;
            while (true)
            {
                if (await LeaseResourceExists(name))
                {
                    var olr = await GetLeaseResource(name);
                    if (olr != null)
                    {
                        _log.Debug("Lease {0} already exists. Returning {1}", name, olr);
                        return olr;
                    }
                }
                else
                {
                    _log.Info("lease {0} does not exist, creating", name);
                    var olr = await CreateLeaseResource(name);
                    if (olr != null)
                        return olr;
                }
                
                tries++;
                if (tries >= maxTries)
                    throw new LeaseException($"Unable to create or read lease after {maxTries} tries");
            }
            
        }
        
        private async Task<BlobContainerClient> ContainerClient()
        {
            var serviceClient = _settings.AzureCredential != null && _settings.ServiceEndpoint != null
                ? new BlobServiceClient(_settings.ServiceEndpoint, _settings.AzureCredential, _settings.BlobClientOptions)
                : new BlobServiceClient(_settings.ConnectionString);
            
            var client = serviceClient.GetBlobContainerClient(_settings.ContainerName);
            
            // Ensure container exists. Only attempted once per AzureApiImpl instance.
            //
            // Uses CreateAsync() instead of CreateIfNotExistsAsync() because the latter has known
            // Azure SDK bugs where it still throws RequestFailedException(409).
            // See: https://github.com/Azure/azure-sdk-for-net/issues/28549
            if (!_initialized)
            {
                try
                {
                    await client.CreateAsync();
                }
                catch (RequestFailedException ex)
                {
                    switch ((HttpStatusCode)ex.Status)
                    {
                        case HttpStatusCode.Conflict when ex.ErrorCode == "ContainerAlreadyExists":
                            // Benign — container already exists from a previous run or another node.
                            _log.Debug("Container '{0}' already exists", _settings.ContainerName);
                            break;

                        case HttpStatusCode.Conflict:
                            // ContainerBeingDeleted or other transient 409 — retriable
                            throw new ContainerInitializationException(
                                $"Container '{_settings.ContainerName}' creation conflict: {ex.ErrorCode}",
                                ex.Status, ex.ErrorCode, ex);

                        case HttpStatusCode.Forbidden:
                        case HttpStatusCode.Unauthorized:
                            throw new ContainerInitializationException(
                                $"Not authorized to create container '{_settings.ContainerName}': [{ex.ErrorCode}]",
                                ex.Status, ex.ErrorCode, ex);

                        default:
                            // Other errors (429, 500, 503, etc.)
                            throw new ContainerInitializationException(
                                $"Container '{_settings.ContainerName}' creation failed with status {ex.Status}: {ex.ErrorCode}",
                                ex.Status, ex.ErrorCode, ex);
                    }
                }
                _initialized = true;
            }
            
            return client;
        }

        [InternalApi]
        public async Task<Either<LeaseResource, LeaseResource>> UpdateLeaseResource(
            string leaseName, string ownerName, ETag version, DateTimeOffset? time = null)
        {
            using var cts = new CancellationTokenSource(_settings.ApiServiceRequestTimeout);
            try
            {
                var leaseBody = new LeaseBody(ownerName, time);
                _log.Debug("Updating {0} to {1}", leaseName, leaseBody);
                
                var blobClient = (await ContainerClient()).GetBlobClient(leaseName);
                var options = new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions
                    {
                        IfMatch = version
                    }
                };
                var operationResponse = await blobClient.UploadAsync(
                        content: new BinaryData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(leaseBody))), 
                        options: options, 
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false);

                var newLease = ToLeaseResource(leaseBody, operationResponse);
                _log.Debug("Lease after update: {0}", newLease);
                return new Right<LeaseResource, LeaseResource>(newLease);
            }
            catch (RequestFailedException e)
            {
                switch ((HttpStatusCode) e.Status)
                {
                    case HttpStatusCode.PreconditionFailed:
                    case HttpStatusCode.Conflict:
                        var oldLease = await GetLeaseResource(leaseName);
                        if (oldLease == null)
                            throw new LeaseException($"GET after PUT conflict did not return a lease. Lease[{leaseName}-{ownerName}]", e);
                        
                        _log.Debug(e, "LeaseResource read after conflict: {0}", oldLease);
                        return new Left<LeaseResource, LeaseResource>(oldLease);

                    case HttpStatusCode.Forbidden:
                        throw new LeaseException(
                            $"Forbidden to communicate with Azure Blob server to update lease {leaseName} for {ownerName}. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            $"Unauthorized to communicate with Azure Blob server to update lease {leaseName} for {ownerName}. " +
                            $"Reason: [{e.ErrorCode}]", e);

                    case var unexpected:
                        throw new LeaseException(
                            $"PUT for lease {leaseName} returned unexpected status code ${unexpected}. " +
                            $"Reason: [{e.ErrorCode}]", e);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out updating lease {leaseName} to owner {ownerName}. It is not known if the update happened.", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out updating lease {leaseName} to owner {ownerName}. It is not known if the update happened.", e);
            }
        }

        private async Task<LeaseResource?> CreateLeaseResource(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.ApiServiceRequestTimeout);
            try
            {
                var blobClient = (await ContainerClient()).GetBlobClient(leaseName);
                var leaseBody = new LeaseBody();
                var operationResponse = await blobClient.UploadAsync(
                        content: new BinaryData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(leaseBody))),
                        overwrite: false,
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false);

                _log.Debug("Lease resource {0} created", leaseName);
                return ToLeaseResource(leaseBody, operationResponse);
            }
            catch (ContainerInitializationException e)
            {
                _log.Warning(e, "Container initialization failed while creating lease {0}: {1}", leaseName, e.Message);
                return null;
            }
            catch (RequestFailedException e)
            {
                switch ((HttpStatusCode)e.Status)
                {
                    case HttpStatusCode.PreconditionFailed:
                    case HttpStatusCode.Conflict:
                        _log.Debug(e,
                            "Creation of lease resource {0} failed as already exists. Will attempt to read again", leaseName);
                        // someone else has created it
                        return null;

                    case HttpStatusCode.Forbidden:
                        throw new LeaseException(
                            $"Forbidden to communicate with Azure Blob server when creating lease {leaseName}. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            $"Unauthorized to communicate with Azure Blob server when creating lease {leaseName}. " +
                            $"Reason: [{e.ErrorCode}]", e);

                    case var unexpected:
                        throw new LeaseException(
                            $"Unexpected failure response from API server when creating lease {leaseName}. " +
                            $"StatusCode: [{unexpected}: {e.ErrorCode}]", e);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Lease {leaseName} creation cancelled.", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out creating lease {leaseName}", e);
            }
        }

        private async Task<bool> LeaseResourceExists(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.ApiServiceRequestTimeout);
            try
            {
                var blobClient = (await ContainerClient()).GetBlobClient(leaseName);
                var response = await blobClient.ExistsAsync(cts.Token);
                return response.Value;
            }
            catch (ContainerInitializationException e)
            {
                _log.Warning(e, "Container initialization failed while checking lease {0} existence: {1}", leaseName, e.Message);
                return false;
            }
            catch (RequestFailedException e)
            {
                throw (HttpStatusCode)e.Status switch
                {
                    HttpStatusCode.Forbidden => new LeaseException(
                        $"Forbidden to communicate with Azure Blob server when checking lease {leaseName} existence. " + 
                        $"Reason: [{e.ErrorCode}]", e),
                    HttpStatusCode.Unauthorized => new LeaseException(
                        $"Unauthorized to communicate with Azure Blob server when checking lease {leaseName} existence. " + 
                        $"Reason: [{e.ErrorCode}]", e),
                    var unexpected => new LeaseException(
                        $"Unexpected failure response from API server when checking lease {leaseName} existence. " +
                        $"StatusCode: [${unexpected}: {e.ErrorCode}]", e)
                };
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Exist check cancelled for lease {leaseName}", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out checking lease {leaseName} existence", e);
            }
        }
        
        
        private async Task<LeaseResource?> GetLeaseResource(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.ApiServiceRequestTimeout);
            try
            {
                var blobClient = (await ContainerClient()).GetBlobClient(leaseName);
                var operationResponse = await blobClient.DownloadAsync(cts.Token);

                // it exists, parse it
                var lease = ToLeaseResource(operationResponse.Value);
                _log.Debug("Resource {0} exists: {1}", leaseName, lease);
                return lease;
            }
            catch (ContainerInitializationException e)
            {
                _log.Warning(e, "Container initialization failed while retrieving lease {0}: {1}", leaseName, e.Message);
                return null;
            }
            catch (RequestFailedException e)
            {
                switch ((HttpStatusCode) e.Status)
                {
                    case HttpStatusCode.NotFound:
                        _log.Debug(e, "Lease resource does not exist: {0}", leaseName);
                        return null;

                    case HttpStatusCode.Forbidden:
                        throw new LeaseException(
                            $"Forbidden to communicate with Azure Blob server when retrieving lease {leaseName}. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            $"Unauthorized to communicate with Azure Blob server when retrieving lease {leaseName}. " +
                            $"Reason: [{e.ErrorCode}]", e);

                    case var unexpected:
                        throw new LeaseException(
                            $"Unexpected failure response from API server when retrieving lease {leaseName}. " +
                            $"StatusCode: [${unexpected}: {e.ErrorCode}]", e);
                }
            }
            catch (InvalidOperationException e)
            {
                throw new LeaseException($"Failed to retrieve lease {leaseName}", e);
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Retrieving lease {leaseName} cancelled", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out retrieving lease {leaseName}", e);
            }
        }

        internal async Task<Done> RemoveLease(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.ApiServiceRequestTimeout);
            try
            {
                var blobClient = (await ContainerClient()).GetBlobClient(leaseName);
                await blobClient.DeleteAsync(cancellationToken: cts.Token)
                    .ConfigureAwait(false);
                
                _log.Debug("Lease deleted: {0}", leaseName);
                return Done.Instance;
            }
            catch (RequestFailedException e)
            {
                switch ((HttpStatusCode) e.Status)
                {
                    case HttpStatusCode.NotFound:
                        _log.Debug(e, "Lease does not exist: {0}", leaseName);
                        return Done.Instance;

                    case HttpStatusCode.Forbidden:
                        throw new LeaseException(
                            $"Forbidden to communicate with Azure Blob server while deleting lease {leaseName}. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            $"Unauthorized to communicate with Azure Blob server while deleting lease {leaseName}. " +
                            $"Reason: [{e.ErrorCode}]", e);

                    case var unexpected:
                        throw new LeaseException(
                            $"Unexpected response from API server when deleting lease {leaseName}. " +
                            $"StatusCode: [${unexpected}: {e.ErrorCode}]", e);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out deleting lease {leaseName}. It is not known if the remove operation happened", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out deleting lease {leaseName}. It is not known if the remove operation happened", e);
            }
        }
        
        private static LeaseResource ToLeaseResource(LeaseBody body, Response<BlobContentInfo> response)
            => new (body, response.Value.ETag);

        private static LeaseResource ToLeaseResource(BlobDownloadInfo response)
        {
            using var reader = new StreamReader(response.Content);
            var body = reader.ReadToEnd();
            
            var lease = JsonConvert.DeserializeObject<LeaseBody>(body);
            if (lease is null)
                throw new InvalidOperationException($"Failed to deserialize blob to LeaseBody. Response: [{body}]");
            
            return new LeaseResource(lease, response.Details.ETag);
        }
    }
}