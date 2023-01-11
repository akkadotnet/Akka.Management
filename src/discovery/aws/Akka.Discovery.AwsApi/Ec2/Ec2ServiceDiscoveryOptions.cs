// -----------------------------------------------------------------------
//  <copyright file="Ec2ServiceDiscoveryOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Hosting;
using Amazon.EC2;
using Amazon.EC2.Model;

namespace Akka.Discovery.AwsApi.Ec2;

public class Ec2ServiceDiscoveryOptions: IHoconOption
{
    private const string FullPath = "akka.discovery.aws-api-ec2-tag-based";
    
    public string ConfigPath { get; } = "aws-api-ec2-tag-based";
    public Type Class { get; } = typeof(Ec2TagBasedServiceDiscovery);
    
    /// <summary>
    ///     A class <see cref="Type"/> that extends <see cref="AmazonEC2Config"/> with either 
    ///     a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
    /// </summary>
    public Type? ClientConfig { get; set; }
    
    /// <summary>
    ///     A class <see cref="Type"/> that extends <see cref="Ec2CredentialProvider"/> with either 
    ///     a no arguments constructor or a single argument constructor that takes an ExtendedActorSystem
    /// </summary>
    public Type? CredentialsProvider { get; set; }
    
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

    public void Apply(AkkaConfigurationBuilder builder, Setup? setup = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{FullPath} {{");
        sb.AppendLine($"class = {Class.AssemblyQualifiedName!.ToHocon()}");
        
        if(ClientConfig is { })
        {
            ValidateType<AmazonEC2Config>(ClientConfig, nameof(ClientConfig));
            sb.AppendLine($"client-config = {ClientConfig.AssemblyQualifiedName!.ToHocon()}");
        }

        if (CredentialsProvider is { })
        {
            ValidateType<Ec2CredentialProvider>(CredentialsProvider, nameof(CredentialsProvider));
            sb.AppendLine($"credentials-provider = {CredentialsProvider.AssemblyQualifiedName!.ToHocon()}");
        }

        if (TagKey is { })
            sb.AppendLine($"tag-key = {TagKey.ToHocon()}");

        if (Filters is { })
        {
            var filters = Filters
                .SelectMany(f => f.Values.Select(v => (f.Name, Tag: v)))
                .Select(t => $"{t.Name}={t.Tag}");
            sb.AppendLine($"filters = {string.Join(";", filters).ToHocon()}");
        }

        if (Ports is { })
            sb.AppendLine($"ports = [{string.Join(",", Ports)}]");

        if (Endpoint is { })
            sb.AppendLine($"endpoint = {Endpoint.ToHocon()}");
        
        if(Region is { })
            sb.AppendLine($"region = {Region.ToHocon()}");

        sb.AppendLine("}");

        builder.AddHocon(sb.ToString(), HoconAddMode.Prepend);
    }
}