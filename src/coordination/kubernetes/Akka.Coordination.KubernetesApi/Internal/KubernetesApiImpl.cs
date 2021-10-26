using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Annotations;
using Akka.Coordination.KubernetesApi.Models;
using Akka.Event;
using Akka.Util;
using k8s;
using k8s.Authentication;
using k8s.Models;
using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;

#nullable enable
namespace Akka.Coordination.KubernetesApi.Internal
{
    internal class KubernetesApiImpl : IKubernetesApi
    {
        private readonly KubernetesSettings _settings;
        private readonly ILoggingAdapter _log;
        private readonly k8s.Kubernetes _client;
        private readonly string _namespace;
        private readonly CustomResourceDefinition _crd;

        private string Namespace =>
            _settings.Namespace
                .DefaultIfNullOrWhitespace(ReadConfigVarFromFileSystem(_settings.NamespacePath, "namespace"))
                .DefaultIfNullOrWhitespace("default");
        
        public KubernetesApiImpl(ActorSystem system, KubernetesSettings settings)
        {
            _settings = settings;
            _log = Logging.GetLogger(system, GetType());
            _namespace = Namespace;
            _crd = CustomResourceDefinition.Create(_namespace);
            
            var config = KubernetesClientConfiguration.BuildDefaultConfig();
            if(!string.IsNullOrWhiteSpace(settings.ApiTokenPath))
                config.TokenProvider = new TokenFileAuth(settings.ApiTokenPath);
            if(!string.IsNullOrWhiteSpace(settings.ApiCaPath))
                config.ClientCertificateFilePath = settings.ApiCaPath;
            config.Namespace = _namespace;

            string scheme = settings.Secure ? "https" : "http";

            var host = Environment.GetEnvironmentVariable(settings.ApiServiceHostEnvName);
            var port = Environment.GetEnvironmentVariable(settings.ApiServicePortEnvName);
            config.Host = $"{scheme}://{host}:{port}";

            _client = new k8s.Kubernetes(config);
            _log.Debug("kubernetes access namespace: {0}. Secure: {1}", _namespace, settings.Secure);
        }
        
        [InternalApi]
        public async Task<LeaseResource> ReadOrCreateLeaseResource(string name)
        {
            // TODO: backoff retry
            var maxTries = 5;
            var tries = 0;
            while (true)
            {
                var olr = await GetLeaseResource(name);
                if (olr != null)
                {
                    _log.Debug("{0} already exists. Returning {1}", name, olr);
                    return olr;
                }
                
                _log.Info("lease {0} does not exist, creating", name);
                olr = await CreateLeaseResource(name);
                if (olr != null)
                    return olr;
                
                tries++;
                if (tries >= maxTries)
                    throw new LeaseException($"Unable to create or read lease after {maxTries} tries");
            }
            
        }

        [InternalApi]
        public async Task<Either<LeaseResource, LeaseResource>> UpdateLeaseResource(
            string leaseName, string ownerName, string version, DateTime? time = null)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                var leaseBody = new LeaseCustomResource(
                    metadata: new V1ObjectMeta(name: leaseName, resourceVersion: version),
                    spec: new LeaseSpec(owner: ownerName, time: time ?? DateTime.UtcNow));
                _log.Debug("Updating {0} to {1}", leaseName, leaseBody);
                using var operationResponse = await _client
                    .ReplaceNamespacedCustomObjectWithHttpMessagesAsync(
                        body: leaseBody,
                        @group: _crd.Group,
                        version: _crd.Version,
                        namespaceParameter: _namespace,
                        plural: _crd.PluralName,
                        name: leaseName,
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false);

                var newLease = operationResponse.Body;
                _log.Debug("Lease after update: {0}", JsonConvert.SerializeObject(newLease));
                return new Right<LeaseResource, LeaseResource>(ToLeaseResource(newLease));
            }
            catch (HttpOperationException e)
            {
                switch (e.Response.StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        var oldLease = await GetLeaseResource(leaseName);
                        if (oldLease == null)
                            throw new LeaseException(
                                $"GET after PUT conflict did not return a lease. Lease[{leaseName}-{ownerName}]");
                        _log.Debug("LeaseResource read after conflict: {0}", oldLease);
                        return new Left<LeaseResource, LeaseResource>(oldLease);

                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Kubernetes API server. " +
                            "See https://doc.akka.io/docs/akka-management/current/kubernetes-lease.html#role-based-access-control for setting up access control. " +
                            $"Reason: [{e.Response.ReasonPhrase}]" +
                            $"Body: {e.Response.Content}");

                    case var unexpected:
                        throw new LeaseException(
                            $"PUT for lease {leaseName} returned unexpected status code ${unexpected}. " +
                            $"Reason: [{e.Response.ReasonPhrase}]" +
                            $"Body: {e.Response.Content}");
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out updating lease {leaseName} to owner {ownerName}. It is not known if the update happened. Is the API server up?", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out updating lease {leaseName} to owner {ownerName}. It is not known if the update happened. Is the API server up?", e);
            }
        }

        internal async Task<LeaseResource?> CreateLeaseResource(string name)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                var leaseBody = new LeaseCustomResource(
                    metadata: new V1ObjectMeta(name: name, namespaceProperty: _namespace),
                    spec: new LeaseSpec(owner: "", time: DateTime.UtcNow));
                using var operationResponse = await _client
                    .CreateNamespacedCustomObjectWithHttpMessagesAsync(
                        leaseBody,
                        _crd.Group,
                        _crd.Version,
                        _namespace,
                        _crd.PluralName,
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false);

                _log.Debug("Lease resource created");
                return ToLeaseResource(operationResponse.Body);
            }
            catch (HttpOperationException e)
            {
                switch (e.Response.StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        _log.Debug("creation of lease resource failed as already exists. Will attempt to read again");
                        // someone else has created it
                        return null;

                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Kubernetes API server. " +
                            "See https://doc.akka.io/docs/akka-management/current/kubernetes-lease.html#role-based-access-control for setting up access control. " +
                            $"Reason: [{e.Response.ReasonPhrase}] " +
                            $"Body: {e.Response.Content}");

                    case var unexpected:
                        throw new LeaseException(
                            "Unexpected response from API server when creating lease. " +
                            $"StatusCode: [{unexpected}] " +
                            $"Reason: [{e.Response.ReasonPhrase}] " +
                            $"Body: {e.Response.Content}");
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out creating lease {name}. Is the API server up?", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out creating lease {name}. Is the API server up?", e);
            }
        }
        
        internal async Task<LeaseResource?> GetLeaseResource(string name)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                using var operationResponse = await _client
                    .GetNamespacedCustomObjectWithHttpMessagesAsync(
                        @group: _crd.Group,
                        version: _crd.Version,
                        namespaceParameter: _namespace,
                        plural: _crd.PluralName,
                        name: name,
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false);
                
                // it exists, parse it
                _log.Debug("Resource {0} exists: {1}", name, operationResponse.Response);
                return ToLeaseResource(operationResponse.Body.ToString());
            }
            catch (HttpOperationException e)
            {
                switch (e.Response.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        _log.Debug("Resource does not exist: {0}", name);
                        return null;

                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Kubernetes API server. " +
                            "See https://doc.akka.io/docs/akka-management/current/kubernetes-lease.html#role-based-access-control for setting up access control. " +
                            $"Reason: [{e.Response.ReasonPhrase}] " +
                            $"Body: {e.Response.Content}");

                    case var unexpected:
                        throw new LeaseException(
                            $"Unexpected response from API server when retrieving lease StatusCode: ${unexpected}. " +
                            $"Reason: [{e.Response.ReasonPhrase}] " +
                            $"Body: {e.Response.Content}");
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out reading lease {name}. Is the API server up?", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out reading lease {name}. Is the API server up?", e);
            }
        }

        internal async Task<Done> RemoveLease(string name)
        {
            var cts = new CancellationTokenSource(_settings.BodyReadTimeout);
            try
            {
                using var operationResponse = await _client
                    .DeleteNamespacedCustomObjectWithHttpMessagesAsync(
                        @group: _crd.Group,
                        version: _crd.Version,
                        namespaceParameter: _namespace,
                        plural: _crd.PluralName,
                        name: name,
                        cancellationToken: cts.Token)
                    .ConfigureAwait(false);
                _log.Debug("Lease deleted: {0}", name);
                return Done.Instance;
            }
            catch (HttpOperationException e)
            {
                switch (e.Response.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        _log.Debug("Lease already deleted: {0}", name);
                        return Done.Instance;

                    case HttpStatusCode.Unauthorized:
                        throw new LeaseException(
                            "Unauthorized to communicate with Kubernetes API server. " +
                            "See https://doc.akka.io/docs/akka-management/current/kubernetes-lease.html#role-based-access-control for setting up access control. " +
                            $"Reason: [{e.Response.ReasonPhrase}] " +
                            $"Body: {e.Response.Content}");

                    case var unexpected:
                        throw new LeaseException(
                            $"Unexpected response from API server when deleting lease StatusCode: ${unexpected}. " +
                            $"Reason: [{e.Response.ReasonPhrase}] " +
                            $"Body: {e.Response.Content}");
                }
            }
            catch (OperationCanceledException e)
            {
                throw new LeaseTimeoutException($"Timed out removing lease {name}. It is not known if the remove happened. Is the API server up?", e);
            }
            catch (TimeoutException e)
            {
                throw new LeaseTimeoutException($"Timed out removing lease {name}. It is not known if the remove happened. Is the API server up?", e);
            }
        }
        
        private LeaseResource ToLeaseResource(object obj)
        {
            var lease = SafeJsonConvert.DeserializeObject<LeaseCustomResource>(obj.ToString());
            _log.Debug("Converting {0}", lease);
            if (lease.Metadata.ResourceVersion == null)
            {
                throw new LeaseException(
                    $"Lease returned from Kubernetes without a resourceVersion: {JsonConvert.SerializeObject(lease)}");
            }

            return new LeaseResource(
                lease.Spec.Owner,
                lease.Metadata.ResourceVersion,
                lease.Spec.Time);
        }
        
        // This uses blocking IO, and so should only be used to read configuration at startup.
        internal virtual string? ReadConfigVarFromFileSystem(string path, string name)
        {
            if (File.Exists(path))
            {
                try
                {
                    return File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    _log.Error(e, "Error reading {0} from {1}", name, path);
                    return null;
                }
            }
            _log.Warning("Unable to read {0} from {1} because it does not exists.", name, path);
            return null;
        }
    }
}