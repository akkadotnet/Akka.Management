//-----------------------------------------------------------------------
// <copyright file="HealthCheckSettings.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using Akka.Configuration;

namespace Akka.Management
{
    public class HealthCheckSettings
    {
        public static HealthCheckSettings Create(Config config)
        {
            bool ValidFqcn(string value)
                => value != null && value != "null" && !string.IsNullOrWhiteSpace(value);
            
            var readiness = config.GetConfig("readiness-checks").AsEnumerable()
                .Select(kvp => (kvp.Key, kvp.Value.GetString()))
                .Where(v => ValidFqcn(v.Item2))
                .Select(v => new NamedHealthCheck(v.Item1, v.Item2))
                .ToImmutableList();
            
            var liveness = config.GetConfig("liveness-checks").AsEnumerable()
                .Select(kvp => (kvp.Key, kvp.Value.GetString()))
                .Where(v => ValidFqcn(v.Item2))
                .Select(v => new NamedHealthCheck(v.Item1, v.Item2))
                .ToImmutableList();

            return new HealthCheckSettings(
                readiness,
                liveness,
                config.GetString("readiness-path"),
                config.GetString("liveness-path"),
                config.GetTimeSpan("check-timeout"));
        }

        public static HealthCheckSettings Create(
            ImmutableList<NamedHealthCheck> readinessChecks,
            ImmutableList<NamedHealthCheck> livenessChecks,
            string readinessPath,
            string livenessPath,
            TimeSpan checkTimeout)
            => new HealthCheckSettings(readinessChecks, livenessChecks, readinessPath, livenessPath, checkTimeout);
        
        public sealed class NamedHealthCheck
        {
            public NamedHealthCheck(string name, string fqcn)
            {
                Name = name;
                Fqcn = fqcn;
            }

            public string Name { get; }
            public string Fqcn { get; }
        }
        
        internal HealthCheckSettings(
            ImmutableList<NamedHealthCheck> readinessChecks, 
            ImmutableList<NamedHealthCheck> livenessChecks, 
            string readinessPath,
            string livenessPath,
            TimeSpan checkTimeout)
        {
            ReadinessChecks = readinessChecks;
            LivenessChecks = livenessChecks;
            ReadinessPath = readinessPath;
            LivenessPath = livenessPath;
            CheckTimeout = checkTimeout;
        }

        public ImmutableList<NamedHealthCheck> ReadinessChecks { get; }
        public ImmutableList<NamedHealthCheck> LivenessChecks { get; }
        public string ReadinessPath { get; }
        public string LivenessPath { get; }
        public TimeSpan CheckTimeout { get; }
    }
}