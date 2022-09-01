//-----------------------------------------------------------------------
// <copyright file="Ec2TagBasedServiceDiscovery.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        internal static ImmutableList<Filter> ParseFiltersString(string filtersString)
        {
            var filters = new List<Filter>();
            
            var kvpList = filtersString.Split(';');
            foreach (var kvp in kvpList)
            {
                if(string.IsNullOrEmpty(kvp))
                    continue;
                
                var pair = kvp.Split('=');
                if (pair.Length != 2)
                    throw new ConfigurationException($"Failed to parse one of the key-value pairs in filters: {kvp}");
                filters.Add(new Filter(pair[0], pair[1].Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).ToList()));
            }

            return filters.ToImmutableList();
        }
        
        private readonly ILoggingAdapter _log;
        private readonly ExtendedActorSystem _system;

        private readonly Ec2ServiceDiscoverySettings _settings;
        
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
                if (_settings.ClientConfig == null)
                {
                    clientConfig = DefaultClientConfiguration;
                }
                else
                {
                    try
                    {
                        clientConfig = CreateInstance<AmazonEC2Config>(_settings.ClientConfig);
                    }
                    catch (Exception e)
                    {
                        throw new ConfigurationException($"Could not create instance of [{_settings.ClientConfig}]", e);
                    }
                }

                if (!string.IsNullOrWhiteSpace(_settings.Endpoint))
                    clientConfig.ServiceURL = _settings.Endpoint;

                if (!string.IsNullOrWhiteSpace(_settings.Region))
                    clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(_settings.Region);

                Ec2CredentialProvider credentialProvider;
                try
                {
                    credentialProvider = CreateInstance<Ec2CredentialProvider>(_settings.CredentialsProvider);
                }
                catch (Exception e)
                {
                    throw new ConfigurationException($"Could not create instance of [{_settings.CredentialsProvider}]", e);
                }
                
                _ec2ClientDoNotUseDirectly = new AmazonEC2Client(credentialProvider.ClientCredentials, clientConfig);
                return _ec2ClientDoNotUseDirectly;
            }
        }
        
        public Ec2TagBasedServiceDiscovery(ExtendedActorSystem system)
        {
            _system = system;
            _log = Logging.GetLogger(system, typeof(Ec2TagBasedServiceDiscovery));
            _settings = Ec2ServiceDiscoverySettings.Create(system);
            
            var setup = system.Settings.Setup.Get<Ec2ServiceDiscoverySetup>();
            if (setup.HasValue)
                _settings = setup.Value.Apply(_settings);
            
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
            var tagFilter = new Filter($"tag:{_settings.TagKey}", new List<string> {query.ServiceName});
            var allFilter = new List<Filter> { _runningInstancesFilter, tagFilter };
            allFilter.AddRange(_settings.Filters);

            var ips = await GetInstances(Ec2Client, allFilter, token);
            var resolvedTargets = new List<ResolvedTarget>();
            foreach (var ip in ips)
            {
                if (_settings.Ports.Count == 0)
                {
                    resolvedTargets.Add(new ResolvedTarget(
                        host: ip, 
                        port: null,
                        address: IPAddress.TryParse(ip, out var ipAddress) ? ipAddress : null));
                }
                else
                {
                    foreach (var port in _settings.Ports)
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
        
        private T CreateInstance<T>(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            
            if(!typeof(T).IsAssignableFrom(type))
                throw new ConfigurationException($"Could not cast type {type} to {typeof(T)}");
            
            try
            {
                return (T) Activator.CreateInstance(type, _system);
            }
            catch (MissingMethodException)
            {
                return (T) Activator.CreateInstance(type);
            }
        }
    }
}