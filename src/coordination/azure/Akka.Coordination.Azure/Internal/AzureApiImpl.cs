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
using Azure.Storage.Blobs.Specialized;
using Newtonsoft.Json;

namespace Akka.Coordination.Azure.Internal
{
    internal class AzureApiImpl: IAzureApi
    {
        private readonly AzureLeaseSettings _settings;
        private readonly ILoggingAdapter _log;
        private readonly Lazy<BlobContainerClient> _containerClient;

        public AzureApiImpl(ActorSystem system, AzureLeaseSettings settings)
        {
            _settings = settings;
            _log = Logging.GetLogger(system, GetType());

            _containerClient = new Lazy<BlobContainerClient>(() =>
            {
                var client = settings.AzureCredential != null && settings.ServiceEndpoint != null
                    ? new BlobContainerClient(settings.ServiceEndpoint, settings.AzureCredential,
                        settings.BlobClientOptions)
                    : new BlobContainerClient(settings.ConnectionString, settings.ContainerName);

                var response = client.Exists();
                if (!response.Value)
                    client.Create();
                
                return client;
            });
        }
        
        [InternalApi]
        public async Task<LeaseResource> ReadOrCreateLeaseResource(string name)
        {
            // TODO: backoff retry
            var maxTries = 5;
            var tries = 0;
            while (true)
            {
                if (await LeaseResourceExists(name))
                {
                    var olr = await GetLeaseResource(name);
                    if (olr != null)
                    {
                        _log.Debug("{0} already exists. Returning {1}", name, olr);
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

        [InternalApi]
        public async Task<Either<LeaseResource, LeaseResource>> UpdateLeaseResource(
            string leaseName, string ownerName, ETag version, DateTimeOffset? time = null)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                _log.Debug("Updating {0}", leaseName);
                
                var blobClient = _containerClient.Value.GetBlobClient(leaseName);
                var leaseBody = new LeaseBody(ownerName, time);
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
                            "Forbidden to communicate with Azure Blob server. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Azure Blob server. " +
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

        private async Task<LeaseResource> CreateLeaseResource(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                var blobClient = _containerClient.Value.GetBlobClient(leaseName);
                var leaseBody = new LeaseBody();
                var operationResponse = await blobClient.UploadAsync(
                        content: new BinaryData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(leaseBody))), 
                        overwrite: false, 
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false);

                _log.Debug("Lease resource created");
                return ToLeaseResource(leaseBody, operationResponse);
            }
            catch (RequestFailedException e)
            {
                switch ((HttpStatusCode) e.Status)
                {
                    case HttpStatusCode.Conflict:
                        _log.Debug(e, "Creation of lease resource failed as already exists. Will attempt to read again");
                        // someone else has created it
                        return null;

                    case HttpStatusCode.Forbidden:
                        throw new LeaseException(
                            "Forbidden to communicate with Azure Blob server. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Kubernetes API server. " +
                            $"Reason: [{e.ErrorCode}]", e);

                    case var unexpected:
                        throw new LeaseException(
                            "Unexpected response from API server when creating lease. " +
                            $"StatusCode: [{unexpected}] Reason: [{e.ErrorCode}]", e);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out creating lease {leaseName}.", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out creating lease {leaseName}", e);
            }
        }

        private async Task<bool> LeaseResourceExists(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                var blobClient = _containerClient.Value.GetBlobClient(leaseName);
                var response = await blobClient.ExistsAsync(cts.Token);
                return response.Value;
            }
            catch (RequestFailedException e)
            {
                throw (HttpStatusCode)e.Status switch
                {
                    HttpStatusCode.Forbidden => new LeaseException(
                        "Forbidden to communicate with Azure Blob server. " + $"Reason: [{e.ErrorCode}]", e),
                    HttpStatusCode.Unauthorized => new LeaseException(
                        "Unauthorized to communicate with Kubernetes API server. " + $"Reason: [{e.ErrorCode}]", e),
                    var unexpected => new LeaseException(
                        $"Unexpected response from API server when retrieving lease StatusCode: ${unexpected}. " +
                        $"Reason: [{e.ErrorCode}]", e)
                };
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out reading lease {leaseName}. Is the API server up?", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out reading lease {leaseName}. Is the API server up?", e);
            }
        }
        
        
        private async Task<LeaseResource> GetLeaseResource(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                var blobClient = _containerClient.Value.GetBlobClient(leaseName);
                var operationResponse = await blobClient.DownloadAsync(cts.Token);

                // it exists, parse it
                var lease = ToLeaseResource(operationResponse.Value);
                _log.Debug("Resource {0} exists: {1}", leaseName, lease);
                return lease;
            }
            catch (RequestFailedException e)
            {
                switch ((HttpStatusCode) e.Status)
                {
                    case HttpStatusCode.NotFound:
                        _log.Debug(e, "Resource does not exist: {0}", leaseName);
                        return null;

                    case HttpStatusCode.Forbidden:
                        throw new LeaseException(
                            "Forbidden to communicate with Azure Blob server. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Kubernetes API server. " +
                            $"Reason: [{e.ErrorCode}]", e);

                    case var unexpected:
                        throw new LeaseException(
                            $"Unexpected response from API server when retrieving lease StatusCode: ${unexpected}. " +
                            $"Reason: [{e.ErrorCode}]", e);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out reading lease {leaseName}. Is the API server up?", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out reading lease {leaseName}. Is the API server up?", e);
            }
        }

        internal async Task<Done> RemoveLease(string leaseName)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                var blobClient = _containerClient.Value.GetBlobClient(leaseName);
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
                            "Forbidden to communicate with Azure Blob server. " +
                            $"Reason: [{e.ErrorCode}]", e);
                        
                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Kubernetes API server. " +
                            $"Reason: [{e.ErrorCode}]", e);

                    case var unexpected:
                        throw new LeaseException(
                            $"Unexpected response from API server when deleting lease StatusCode: ${unexpected}. " +
                            $"Reason: [{e.ErrorCode}]", e);
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out removing lease {leaseName}. It is not known if the remove happened. Is the API server up?", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out removing lease {leaseName}. It is not known if the remove happened. Is the API server up?", e);
            }
        }
        
        private static LeaseResource ToLeaseResource(LeaseBody body, Response<BlobContentInfo> response)
            => new LeaseResource(body, response.Value.ETag);

        private static LeaseResource ToLeaseResource(BlobDownloadInfo response)
        {
            using var reader = new StreamReader(response.Content);
            var body = reader.ReadToEnd();
            
            return new LeaseResource(JsonConvert.DeserializeObject<LeaseBody>(body), response.Details.ETag);
        }
    }
}