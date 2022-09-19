// -----------------------------------------------------------------------
//  <copyright file="Ec2ServiceDiscoverySetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Akka.Discovery.AwsApi.Ec2
{
    public sealed class Ec2ServiceDiscoverySetup : Setup
    {
        private Type _clientConfig;

        public Type ClientConfig
        {
            get => _clientConfig;
            set
            {
                if (value != null)
                    ValidateType<AmazonEC2Config>(value, nameof(ClientConfig));
                _clientConfig = value;
            }
        } 

        private Type _credProvider;
        public Type CredentialsProvider
        {
            get => _credProvider;
            set
            {
                if (value != null)
                    ValidateType<Ec2CredentialProvider>(value, nameof(CredentialsProvider));
                _credProvider = value;
            }
        } 

        public string TagKey { get; set; }
        public List<Filter> Filters { get; set; }
        public List<int> Ports { get; set; }
        public string Endpoint { get; set; }
        public string Region { get; set; }

        public Ec2ServiceDiscoverySetup WithClientConfig<T>() where T: AmazonEC2Config
        {
            var value = typeof(T);
            ValidateType<AmazonEC2Config>(value, nameof(WithClientConfig));
            ClientConfig = value;
            return this;
        }

        public Ec2ServiceDiscoverySetup WithCredentialProvider<T>() where T : Ec2CredentialProvider
        {
            var value = typeof(T);
            ValidateType<Ec2CredentialProvider>(value, nameof(WithCredentialProvider));
            CredentialsProvider = value;
            return this;
        }

        private static void ValidateType<T>(Type type, string paramName)
        {
            if (!typeof(T).IsAssignableFrom(type))
                throw new ConfigurationException($"{paramName} Type value need to extend {typeof(T).Name}. Was: {type.Name}");

            var ctorInfo = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (!(ctorInfo is null)) 
                return;
            
            ctorInfo = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new [] {typeof(ExtendedActorSystem)}, null);
            if (ctorInfo is null)
                throw new ConfigurationException(
                    $"{paramName} Type value need to have a parameterless constructor or one with a single {nameof(ExtendedActorSystem)} parameter");
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