using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using k8s;
using k8s.Authentication;
using k8s.Models;

#nullable enable
namespace Akka.Discovery.KubernetesApi
{
    public class KubernetesApiServiceDiscovery : ServiceDiscovery
    {
        public sealed class KubernetesApiException : Exception
        {
            public KubernetesApiException(string message) : base(message)
            { }
            
            public KubernetesApiException(string message, Exception innerException) : base(message, innerException)
            { }

            public override string StackTrace => "";
        }

        private readonly ILoggingAdapter _log;
        private readonly KubernetesDiscoverySettings _settings;

        private string PodNamespace =>
            _settings.PodNamespace
                .DefaultIfNullOrWhitespace(ReadConfigVarFromFileSystem(_settings.PodNamespacePath, "pod-namespace"))
                .DefaultIfNullOrWhitespace("default");
        
        public KubernetesApiServiceDiscovery(ExtendedActorSystem system)
        {
            _log = Logging.GetLogger(system, this);
            _settings = KubernetesDiscovery.Get(system).Settings;
            
            if(_log.IsDebugEnabled)
                _log.Debug("Settings {0}", _settings);
        }
        
        public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
        {
            var labelSelector = _settings.PodLabelSelector(lookup.ServiceName);
            
            if(_log.IsInfoEnabled)
                _log.Info("Querying for pods with label selector: [{0}]. Namespace: [{1}]. Port: [{2}]",
                    labelSelector, PodNamespace, lookup.PortName);

            var config = KubernetesClientConfiguration.BuildDefaultConfig();
            config.TokenProvider = new TokenFileAuth(_settings.ApiTokenPath);
            config.ClientCertificateFilePath = _settings.ApiCaPath;
            config.Namespace = PodNamespace;

            var host = Environment.GetEnvironmentVariable(_settings.ApiServiceHostEnvName);
            var port = Environment.GetEnvironmentVariable(_settings.ApiServicePortEnvName);
            config.Host = $"{host}:{port}";
            
            var client = new Kubernetes(config);

            V1PodList podList;
            try
            {
                podList = await client.ListNamespacedPodAsync(PodNamespace);
            }
            catch (Exception e)
            {
                throw new KubernetesApiException($"Failed to retrieve pod list from {config.Host}", e);
            }
            
            var addresses = Targets(podList, lookup.PortName, PodNamespace, _settings.PodDomain, _settings.RawIp, _settings.ContainerName).ToList();
            if (addresses.Count == 0 && podList.Items.Count > 0 && _log.IsInfoEnabled)
            {
                var containerPortNames = podList.Items
                    .Select(p => p.Spec)
                    .SelectMany(s => s.Containers)
                    .SelectMany(c => c.Ports)
                    .Select(p => p.Name);
                _log.Info(
                    "No targets found from pod list. Is the correct port name configured? Current configuration: [{0}]. Ports on pods: [{1}]",
                    lookup.PortName,
                    string.Join(", ", containerPortNames));
            }

            return new Resolved(serviceName: lookup.ServiceName, addresses: addresses);
        }
        
        // This uses blocking IO, and so should only be used to read configuration at startup.
        private string? ReadConfigVarFromFileSystem(string path, string name)
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

        internal static IEnumerable<ResolvedTarget> Targets(
            V1PodList podList,
            string? portName,
            string podNamespace,
            string podDomain,
            bool rawIp,
            string? containerName)
        {
            foreach (var item in podList.Items)
            {
                if (item.Metadata.DeletionTimestamp != null) 
                    continue;
                
                var itemStatus = item.Status;
                if (!itemStatus.Phase.ToLowerInvariant().Contains("running")) 
                    continue;
                
                var itemSpec = item.Spec;

                if (containerName != null)
                {
                    if (itemStatus.ContainerStatuses
                        .Where(s => s.Name.Equals(containerName))
                        .Any(s => s.State.Waiting != null)) 
                        continue;
                }
                
                var ip = itemStatus.PodIP;
                if(string.IsNullOrWhiteSpace(ip))
                    continue;
                
                // Maybe port is a nullable of a port, and will be null if no portName was requested
                int? maybePort = null;
                if (portName != null)
                {
                    maybePort = itemSpec.Containers
                        .SelectMany(c => c.Ports)
                        .FirstOrDefault(p => p.Name.Contains(portName))?.ContainerPort;
                }
                
                var hostOrIp = rawIp ? ip : $"{ip.Replace('.', '-')}.{podNamespace}.pod.{podDomain}";
                yield return new ResolvedTarget(
                    host: hostOrIp,
                    port: maybePort,
                    address: IPAddress.Parse(ip));
            }
        }
    }
}