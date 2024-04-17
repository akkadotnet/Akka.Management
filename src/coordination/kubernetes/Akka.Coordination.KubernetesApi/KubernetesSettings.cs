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
        public static readonly KubernetesSettings Empty = new (
            apiCaPath: "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt",
            apiTokenPath: "/var/run/secrets/kubernetes.io/serviceaccount/token",
            apiServiceHostEnvName: "KUBERNETES_SERVICE_HOST",
            apiServicePortEnvName: "KUBERNETES_SERVICE_PORT",
            ns: null,
            namespacePath: "/var/run/secrets/kubernetes.io/serviceaccount/namespace",
            apiServiceRequestTimeout: TimeSpan.FromSeconds(2), // 2/5 of 5 seconds
            secure: true,
            bodyReadTimeout: TimeSpan.FromSeconds(1)); // half of 2 
        
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

        public static KubernetesSettings Create(LeaseSettings settings)
        {
            var config = settings.LeaseConfig;
            var leaseTimeoutSettings = settings.TimeoutSettings;
            var requestTimeoutValue = config.GetStringIfDefined("api-service-request-timeout");
            var apiServerRequestTimeout = !string.IsNullOrWhiteSpace(requestTimeoutValue)
                ? config.GetTimeSpan("api-service-request-timeout")
                : new TimeSpan(leaseTimeoutSettings.OperationTimeout.Ticks * 2 / 5);  // 2/5 gives two API operations + a buffer

            if (apiServerRequestTimeout >= leaseTimeoutSettings.OperationTimeout)
                throw new ConfigurationException(
                    "'api-service-request-timeout can not be greater than 'lease-operation-timeout'");
            
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
 
        public KubernetesSettings WithApiCaPath(string apiCaPath)
            => Copy(apiCaPath: apiCaPath);
        public KubernetesSettings WithApiTokenPath(string apiTokenPath)
            => Copy(apiTokenPath: apiTokenPath);
        public KubernetesSettings WithApiServiceHostEnvName(string apiServiceHostEnvName)
            => Copy(apiServiceHostEnvName: apiServiceHostEnvName);
        public KubernetesSettings WithApiServicePortEnvName(string apiServicePortEnvName)
            => Copy(apiServicePortEnvName: apiServicePortEnvName);
        public KubernetesSettings WithNamespace(string ns)
            => Copy(ns: ns);
        public KubernetesSettings WithNamespacePath(string namespacePath)
            => Copy(namespacePath: namespacePath);
        public KubernetesSettings WithApiServiceRequestTimeout(TimeSpan apiServiceRequestTimeout)
            => Copy(apiServiceRequestTimeout: apiServiceRequestTimeout);
        public KubernetesSettings WithSecure(bool secure)
            => Copy(secure: secure);
        public KubernetesSettings WithBodyReadTimeout(TimeSpan bodyReadTimeout)
            => Copy(bodyReadTimeout: bodyReadTimeout);
        
        private KubernetesSettings Copy(
            string? apiCaPath = null,
            string? apiTokenPath = null,
            string? apiServiceHostEnvName = null,
            string? apiServicePortEnvName = null,
            string? ns = null,
            string? namespacePath = null,
            TimeSpan? apiServiceRequestTimeout = null,
            bool? secure = null,
            TimeSpan? bodyReadTimeout = null)
            => new (
                apiCaPath: apiCaPath ?? ApiCaPath,
                apiTokenPath: apiTokenPath ?? ApiTokenPath,
                apiServiceHostEnvName: apiServiceHostEnvName ?? ApiServiceHostEnvName,
                apiServicePortEnvName: apiServicePortEnvName ?? ApiServicePortEnvName,
                ns: ns ?? Namespace,
                namespacePath: namespacePath ?? NamespacePath,
                apiServiceRequestTimeout: apiServiceRequestTimeout ?? ApiServiceRequestTimeout,
                secure: secure ?? Secure,
                bodyReadTimeout: bodyReadTimeout ?? BodyReadTimeout);
    }
}