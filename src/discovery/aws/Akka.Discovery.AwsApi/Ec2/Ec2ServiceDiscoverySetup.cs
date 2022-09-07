// -----------------------------------------------------------------------
//  <copyright file="Ec2ServiceDiscoverySetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Akka.Actor.Setup;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Akka.Discovery.AwsApi.Ec2
{
    public sealed class Ec2ServiceDiscoverySetup : Setup
    {
        private Type _clientConfig;
        public Type ClientConfig => _clientConfig;

        private Type _credProvider;
        public Type CredentialsProvider => _credProvider;
        public string TagKey { get; set; }
        public List<Filter> Filters { get; set; }
        public List<int> Ports { get; set; }
        public string Endpoint { get; set; }
        public string Region { get; set; }

        public Ec2ServiceDiscoverySetup WithClientConfig<T>() where T: AmazonEC2Config
        {
            _clientConfig = typeof(T);
            return this;
        }

        public Ec2ServiceDiscoverySetup WithCredentialProvider<T>() where T : Ec2CredentialProvider
        {
            _credProvider = typeof(T);
            return this;
        }
        
        internal Ec2ServiceDiscoverySettings Apply(Ec2ServiceDiscoverySettings settings)
        {
            if (ClientConfig != null)
                settings = settings.WithClientConfig(ClientConfig);
            if (CredentialsProvider != null)
                settings = settings.WithCredentialsProvider(CredentialsProvider);
            if (TagKey != null)
                settings = settings.WithTagKey(TagKey);
            if (Filters != null)
                settings = settings.WithFilters(Filters.ToImmutableList());
            if (Ports != null)
                settings = settings.WithPorts(Ports.ToImmutableList());
            if (Endpoint != null)
                settings = settings.WithEndpoint(Endpoint);
            if (Region != null)
                settings = settings.WithRegion(Region);
            return settings;
        }
    }
}