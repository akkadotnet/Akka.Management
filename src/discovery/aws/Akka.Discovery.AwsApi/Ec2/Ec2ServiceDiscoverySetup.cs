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
        private Type? _clientConfig;

        /// <summary>
        ///     A class <see cref="Type"/> that extends <see cref="AmazonEC2Config"/> with either 
        ///     a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
        /// </summary>
        /// <exception cref="ConfigurationException">
        ///     The class <see cref="Type"/> did not extend <see cref="AmazonEC2Config"/>
        /// </exception>
        public Type? ClientConfig
        {
            get => _clientConfig;
            set
            {
                if (value != null)
                    ValidateType<AmazonEC2Config>(value, nameof(ClientConfig));
                _clientConfig = value;
            }
        } 

        private Type? _credProvider;
        
        /// <summary>
        ///     A class <see cref="Type"/> that extends <see cref="Ec2CredentialProvider"/> with either 
        ///     a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
        /// </summary>
        /// <exception cref="ConfigurationException">
        ///     The class <see cref="Type"/> did not extend <see cref="Ec2CredentialProvider"/>
        /// </exception>
        public Type? CredentialsProvider
        {
            get => _credProvider;
            set
            {
                if (value != null)
                    ValidateType<Ec2CredentialProvider>(value, nameof(CredentialsProvider));
                _credProvider = value;
            }
        } 

        /// <summary>
        ///     The tag name used on the EC2 instances to filter the ones to be considered as possible contact points
        /// </summary>
        public string? TagKey { get; set; }
        
        /// <summary>
        ///     Additional filtering rules to be applied to the possible EC2 contact points
        /// </summary>
        public List<Filter>? Filters { get; set; }
        
        /// <summary>
        ///     List of ports to be considered as Akka.Management ports on each instance.
        ///     Use this if you have multiple Akka.NET nodes per EC2 instance
        /// </summary>
        public List<int>? Ports { get; set; }
        
        /// <summary>
        /// <para>
        ///     Client may use specified endpoint for example ec2.us-west-1.amazonaws.com, 
        ///     region is automatically extrapolated from the endpoint URL
        /// </para>
        ///     NOTE: You can only set either an endpoint OR a region, not both. Region will always win if both are declared.
        /// </summary>
        public string? Endpoint { get; set; }
        
        /// <summary>
        /// <para>
        ///     Client may use specified region for example us-west-1, endpoints are automatically generated.
        /// </para>
        ///     NOTE: You can only set either an endpoint OR a region, not both. Region will always win if both are declared.
        /// </summary>
        public string? Region { get; set; }

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
            var value = typeof(T);
            ValidateType<AmazonEC2Config>(value, nameof(WithClientConfig));
            ClientConfig = value;
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
            if (ctorInfo is not null) 
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