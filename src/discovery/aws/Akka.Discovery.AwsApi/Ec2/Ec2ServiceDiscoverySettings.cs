// -----------------------------------------------------------------------
//  <copyright file="Ec2ServiceDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using Akka.Actor;
using Akka.Configuration;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Akka.Discovery.AwsApi.Ec2
{
    public class Ec2ServiceDiscoverySettings
    {
        public static readonly Ec2ServiceDiscoverySettings Empty = new Ec2ServiceDiscoverySettings(
            null,
            "instance-metadata-credential-provider",
            "service",
            ImmutableList<Filter>.Empty,
            ImmutableList<int>.Empty, 
            null, 
            null);
        
        public static Ec2ServiceDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config.GetConfig("akka.discovery.aws-api-ec2-tag-based"));
        
        public static Ec2ServiceDiscoverySettings Create(Configuration.Config config)
        {
            Type clientConfigType = null;
            var clientConfigTypeName = config.GetString("client-config");
            if (!string.IsNullOrWhiteSpace(clientConfigTypeName))
            {
                clientConfigType = Type.GetType(clientConfigTypeName);
                if (clientConfigType == null || !typeof(AmazonEC2Config).IsAssignableFrom(clientConfigType))
                {
                    throw new ConfigurationException(
                        "client-config must be a fully qualified class name of a class type that extends Amazon.EC2.AmazonEC2Config");
                }
            }

            return new Ec2ServiceDiscoverySettings(
                clientConfigType,
                config.GetString("credentials-provider"),
                config.GetString("tag-key"),
                Ec2TagBasedServiceDiscovery.ParseFiltersString(config.GetString("filters")),
                config.GetIntList("ports").ToImmutableList(),
                config.GetString("endpoint"),
                config.GetString("region")
            );
        }
        
        public Ec2ServiceDiscoverySettings(
            Type clientConfig,
            string credentialsProvider,
            string tagKey, 
            ImmutableList<Filter> filters,
            ImmutableList<int> ports,
            string endpoint,
            string region)
        {
            ClientConfig = clientConfig;
            CredentialsProvider = credentialsProvider;
            TagKey = tagKey;
            Filters = filters;
            Ports = ports;
            Endpoint = endpoint;
            Region = region;
        }

        public Type ClientConfig { get; }
        public string CredentialsProvider { get; }
        public string TagKey { get; }
        public ImmutableList<Filter> Filters { get; }
        public ImmutableList<int> Ports { get; }
        public string Endpoint { get; }
        public string Region { get; }

        internal Ec2ServiceDiscoverySettings WithClientConfig(Type clientConfig)
            => Copy(clientConfig: clientConfig);
        public Ec2ServiceDiscoverySettings WithClientConfig<T>() where T: AmazonEC2Config
            => Copy(clientConfig: typeof(T));
        
        public Ec2ServiceDiscoverySettings WithCredentialsProvider(string providerPath)
            => Copy(credentialsProvider: providerPath);
        
        public Ec2ServiceDiscoverySettings WithTagKey(string tagKey)
            => Copy(tagKey: tagKey);
        
        public Ec2ServiceDiscoverySettings WithFilters(ImmutableList<Filter> filters)
            => Copy(filters: filters);
        
        public Ec2ServiceDiscoverySettings WithPorts(ImmutableList<int> ports)
            => Copy(ports: ports);
        
        public Ec2ServiceDiscoverySettings WithEndpoint(string endpoint)
            => Copy(endpoint: endpoint);
        
        public Ec2ServiceDiscoverySettings WithRegion(string region)
            => Copy(region: region);
        
        private Ec2ServiceDiscoverySettings Copy(
            Type clientConfig = null,
            string credentialsProvider = null,
            string tagKey = null,
            ImmutableList<Filter> filters = null,
            ImmutableList<int> ports = null,
            string endpoint = null,
            string region = null)
            => new Ec2ServiceDiscoverySettings(
                clientConfig: clientConfig ?? ClientConfig,
                credentialsProvider: credentialsProvider ?? CredentialsProvider,
                tagKey: tagKey ?? TagKey,
                filters: filters ?? Filters,
                ports: ports ?? Ports,
                endpoint: endpoint ?? Endpoint,
                region: region ?? Region);
    }
}