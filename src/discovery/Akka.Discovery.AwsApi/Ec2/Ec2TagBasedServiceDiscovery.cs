using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Akka.Discovery.AwsApi.Ec2
{
    public sealed class Ec2TagBasedServiceDiscovery : ServiceDiscovery
    {
        private static List<Filter> ParseFiltersString(string filtersString)
        {
            var filters = new List<Filter>();
            
            var kvpList = filtersString.Split(';');
            foreach (var kvp in kvpList)
            {
                var pair = kvp.Split('=');
                if (pair.Length != 2)
                    throw new ConfigurationException($"Failed to parse one of the key-value pairs in filters: {kvp}");
                filters.Add(new Filter(pair[0], pair[1].Split(',').ToList()));
            }

            return filters;
        }
        
        private readonly ILoggingAdapter _log;
        private readonly ExtendedActorSystem _system;
        private readonly Configuration.Config _config;
        private readonly string _clientConfigFqcn;
        private readonly string _tagKey;
        private readonly List<Filter> _otherFilters;
        private readonly List<int> _preDefinedPorts;
        private readonly Filter _runningInstancesFilter;
        
        // JVM has its own retry mechanism in cluster bootstrap, but we don't have any, so keep the default.
        private AmazonEC2Config DefaultClientConfiguration => new AmazonEC2Config();

        private AmazonEC2Client _ec2ClientDoNotUseDirectly;
        private AmazonEC2Client Ec2Client
        {
            get
            {
                if (_ec2ClientDoNotUseDirectly != null) 
                    return _ec2ClientDoNotUseDirectly;
                
                AmazonEC2Config clientConfig;
                if (string.IsNullOrWhiteSpace(_clientConfigFqcn))
                {
                    clientConfig = DefaultClientConfiguration;
                }
                else
                {
                    try
                    {
                        clientConfig = GetCustomClientConfigurationInstance(_clientConfigFqcn);
                    }
                    catch (Exception e)
                    {
                        throw new ConfigurationException($"Could not create instance of [{_clientConfigFqcn}]", e);
                    }
                }

                if (_config.HasPath("region"))
                {
                    var region = _config.GetString("region");
                    clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
                }
                
                _ec2ClientDoNotUseDirectly = new AmazonEC2Client(clientConfig);
                return _ec2ClientDoNotUseDirectly;
            }
        }
        
        public Ec2TagBasedServiceDiscovery(ExtendedActorSystem system)
        {
            _system = system;
            _log = Logging.GetLogger(system, typeof(Ec2TagBasedServiceDiscovery));
            _config = _system.Settings.Config.GetConfig("akka.discovery.aws-api-ec2-tag-based");
            _clientConfigFqcn = _config.GetString("client-config", null);
            _tagKey = _config.GetString("tag-key");
            var otherFiltersString = _config.GetString("filters");
            _otherFilters = ParseFiltersString(otherFiltersString);
            _preDefinedPorts = _config.GetIntList("ports").ToList();
            _runningInstancesFilter = new Filter("instance-state-name", new List<string> {"running"});
        }
        
        public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
        {
            using(var cts = new CancellationTokenSource(resolveTimeout))
            {
                try
                {
                    return await Lookup(lookup, cts.Token);
                }
                catch (TaskCanceledException e)
                {
                    throw new TaskCanceledException($"Lookup for [{lookup}] timed out, within [{resolveTimeout}]", e);
                }
            }
        }

        private async Task<Resolved> Lookup(Lookup query, CancellationToken token)
        {
            var tagFilter = new Filter($"tag:{_tagKey}", new List<string> {query.ServiceName});
            var allFilter = new List<Filter> { _runningInstancesFilter, tagFilter };
            allFilter.AddRange(_otherFilters);

            var ips = await GetInstances(Ec2Client, allFilter, token);
            var resolvedTargets = new List<ResolvedTarget>();
            foreach (var ip in ips)
            {
                ResolvedTarget resolvedTarget; 
                if (_preDefinedPorts.Count == 0)
                {
                    resolvedTargets.Add(new ResolvedTarget(
                        host: ip, 
                        port: null,
                        address: IPAddress.TryParse(ip, out var ipAddress) ? ipAddress : null));
                }
                else
                {
                    foreach (var port in _preDefinedPorts)
                    {
                        resolvedTargets.Add(new ResolvedTarget(
                            host: ip, 
                            port: port,
                            address: IPAddress.TryParse(ip, out var ipAddress) ? ipAddress : null));
                    }
                }
            }

            return new Resolved(query.ServiceName, resolvedTargets);
        }
        
        private async Task<List<string>> GetInstances(
            AmazonEC2Client client, 
            List<Filter> filters, 
            CancellationToken token)
        {
            var accumulator = new List<string>();
            var describeInstancesRequest = new DescribeInstancesRequest { Filters = filters };
            do
            {
                var describeInstancesResult = await client.DescribeInstancesAsync(describeInstancesRequest, token);
                var ips = (from reservation in describeInstancesResult.Reservations
                           from instance in reservation.Instances
                           select instance.PrivateIpAddress)
                    .ToList();
                accumulator.AddRange(ips);
                describeInstancesRequest.NextToken = describeInstancesResult.NextToken;
                
                if (_log.IsDebugEnabled && describeInstancesRequest.NextToken != null)
                {
                    _log.Debug("AWS API returned paginated result, fetching next page.");
                }
            } while (describeInstancesRequest.NextToken != null);

            return accumulator;
        }
        
        private AmazonEC2Config GetCustomClientConfigurationInstance(string fqcn)
        {
            var type = Type.GetType(fqcn);
            try
            {
                return (AmazonEC2Config) Activator.CreateInstance(type, new object[]{_system});
            }
            catch (MissingMethodException)
            {
                return (AmazonEC2Config) Activator.CreateInstance(type);
            }
        }
    }
}