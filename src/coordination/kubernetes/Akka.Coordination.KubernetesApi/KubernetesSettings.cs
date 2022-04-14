//-----------------------------------------------------------------------
// <copyright file="KubernetesSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Configuration;

#nullable enable
namespace Akka.Coordination.KubernetesApi
{
    public class KubernetesSettings
    {
        public KubernetesSettings(
            string apiCaPath,
            string apiTokenPath,
            string apiServiceHostEnvName,
            string apiServicePortEnvName,
            string? ns,
            string namespacePath,
            TimeSpan apiServiceRequestTimeout,
            bool? secure,
            TimeSpan? bodyReadTimeout = null)
        {
            ApiCaPath = apiCaPath;
            ApiTokenPath = apiTokenPath;
            ApiServiceHostEnvName = apiServiceHostEnvName;
            ApiServicePortEnvName = apiServicePortEnvName;
            Namespace = ns;
            NamespacePath = namespacePath;
            ApiServiceRequestTimeout = apiServiceRequestTimeout;
            Secure = secure ?? true;
            BodyReadTimeout = bodyReadTimeout ?? TimeSpan.FromSeconds(1);
        }

        public static KubernetesSettings Create(ActorSystem system, TimeoutSettings leaseTimeoutSettings)
            => Create(system.Settings.Config.GetConfig(KubernetesLease.ConfigPath), leaseTimeoutSettings);
        
        public static KubernetesSettings Create(Config config, TimeoutSettings leaseTimeoutSettings)
        {
            var requestTimeoutValue = config.GetStringIfDefined("api-service-request-timeout");
            var apiServerRequestTimeout = !string.IsNullOrWhiteSpace(requestTimeoutValue)
                ? config.GetTimeSpan("api-service-request-timeout")
                : new TimeSpan(leaseTimeoutSettings.OperationTimeout.Ticks * 2 / 5);  // 2/5 gives two API operations + a buffer

            if (apiServerRequestTimeout >= leaseTimeoutSettings.OperationTimeout)
                throw new ConfigurationException(
                    "'api-service-request-timeout can not be less than 'lease-operation-timeout'");
            
            var secureValue = config.GetStringIfDefined("secure-api-server");
            var secure = string.IsNullOrWhiteSpace(secureValue) ? (bool?) null : config.GetBoolean("secure-api-server");
            
            return new KubernetesSettings(
                config.GetString("api-ca-path"),
                config.GetString("api-token-path"),
                config.GetString("api-service-host-env-name"),
                config.GetString("api-service-port-env-name"),
                config.GetStringIfDefined("namespace"),
                config.GetString("namespace-path"),
                apiServerRequestTimeout,
                secure,
                new TimeSpan(apiServerRequestTimeout.Ticks / 2)
            );
        } 
        
        public string ApiCaPath { get; }
        public string ApiTokenPath { get; }
        public string ApiServiceHostEnvName { get; }
        public string ApiServicePortEnvName { get; }
        public string? Namespace { get; }
        public string NamespacePath { get; }
        public TimeSpan ApiServiceRequestTimeout { get; }
        public bool Secure { get; }
        public TimeSpan BodyReadTimeout { get; }
 
    }
}