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
using Akka.Configuration;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Akka.Discovery.AwsApi.Ec2
{
    public sealed class Ec2ServiceDiscoverySetup : Setup
    {
        private Type _clientConfig;

        /// <summary>
        ///     A class <see cref="Type"/> that extends <see cref="AmazonEC2Config"/> with either 
        ///     a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
        /// </summary>
        /// <exception cref="ConfigurationException">
        ///     The class <see cref="Type"/> did not extend <see cref="AmazonEC2Config"/>
        /// </exception>
        public Type ClientConfig
        {
            get => _clientConfig;
            set
            {
                if (value != null && !typeof(AmazonEC2Config).IsAssignableFrom(value))
                    throw new ConfigurationException($"{nameof(ClientConfig)} Type value need to extend {nameof(AmazonEC2Config)}. Was: {value.Name}");
                _clientConfig = value;
            }
        } 

        private Type _credProvider;
        
        /// <summary>
        ///     A class <see cref="Type"/> that extends <see cref="Ec2CredentialProvider"/> with either 
        ///     a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
        /// </summary>
        /// <exception cref="ConfigurationException">
        ///     The class <see cref="Type"/> did not extend <see cref="Ec2CredentialProvider"/>
        /// </exception>
        public Type CredentialsProvider
        {
            get => _credProvider;
            set
            {
                if (value != null && !typeof(Ec2CredentialProvider).IsAssignableFrom(value))
                    throw new ConfigurationException($"{nameof(CredentialsProvider)} Type value need to extend {nameof(Ec2CredentialProvider)}. Was: {value.Name}");
                _credProvider = value;
            }
        } 

        /// <summary>
        ///     The tag name used on the EC2 instances to filter the ones to be considered as possible contact points
        /// </summary>
        public string TagKey { get; set; }
        
        /// <summary>
        ///     Additional filtering rules to be applied to the possible EC2 contact points
        /// </summary>
        public List<Filter> Filters { get; set; }
        
        /// <summary>
        ///     List of ports to be considered as Akka.Management ports on each instance.
        ///     Use this if you have multiple Akka.NET nodes per EC2 instance
        /// </summary>
        public List<int> Ports { get; set; }
        
        /// <summary>
        /// <para>
        ///     Client may use specified endpoint for example ec2.us-west-1.amazonaws.com, 
        ///     region is automatically extrapolated from the endpoint URL
        /// </para>
        ///     NOTE: You can only set either an endpoint OR a region, not both. Region will always win if both are declared.
        /// </summary>
        public string Endpoint { get; set; }
        
        /// <summary>
        /// <para>
        ///     Client may use specified region for example us-west-1, endpoints are automatically generated.
        /// </para>
        ///     NOTE: You can only set either an endpoint OR a region, not both. Region will always win if both are declared.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        ///     Sets the <see cref="ClientConfig"/> property, static typed.
        /// </summary>
        /// <typeparam name="T">
        ///     A <see cref="Type"/> that extends <see cref="AmazonEC2Config"/>
        /// </typeparam>
        /// <returns>
        ///     This <see cref="Ec2ServiceDiscoverySetup"/> instance
        /// </returns>
        public Ec2ServiceDiscoverySetup WithClientConfig<T>() where T: AmazonEC2Config
        {
            ClientConfig = typeof(T);
            return this;
        }

        /// <summary>
        ///     Sets the <see cref="ClientConfig"/> property, static typed.
        /// </summary>
        /// <typeparam name="T">
        ///     A <see cref="Type"/> that extends <see cref="Ec2CredentialProvider"/>
        /// </typeparam>
        /// <returns>
        ///     This <see cref="Ec2ServiceDiscoverySetup"/> instance
        /// </returns>
        public Ec2ServiceDiscoverySetup WithCredentialProvider<T>() where T : Ec2CredentialProvider
        {
            CredentialsProvider = typeof(T);
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