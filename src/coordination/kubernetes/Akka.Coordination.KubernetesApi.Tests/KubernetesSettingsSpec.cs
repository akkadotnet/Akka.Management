//-----------------------------------------------------------------------
// <copyright file="KubernetesSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using FluentAssertions;
using Xunit;

#nullable enable
namespace Akka.Coordination.KubernetesApi.Tests
{
    public class KubernetesSettingsSpec
    {
        private KubernetesSettings Conf(string overrides)
        {
            var config = ConfigurationFactory.ParseString(overrides)
                .WithFallback(KubernetesLease.DefaultConfiguration)
                .WithFallback(LeaseProvider.DefaultConfig());
            return KubernetesSettings.Create(config, TimeoutSettings.Create(config.GetConfig("akka.coordination.lease")));
        }
        
        [Fact(DisplayName = "default request-timeout should be 2/5 of the lease-operation-timeout")]
        public void RequestTimeoutIsTwoFifthOfLeaseOperationTimeout()
        {
            Conf("lease-operation-timeout=5s").ApiServiceRequestTimeout.Should().Be(TimeSpan.FromSeconds(2));
        }

        [Fact(DisplayName = "default body-read timeout should be 1/2 of api request timeout")]
        public void BodyReadTimeoutIsHalfOfApiRequestTimeout()
        {
            Conf("lease-operation-timeout=5s").BodyReadTimeout.Should().Be(TimeSpan.FromSeconds(1));
        }

        [Fact(DisplayName = "Kubernetes settings should allow api server request timeout override")]
        public void ShouldAllowServerRequestTimeoutOverride()
        {
            Conf(@"
            lease-operation-timeout=5s
            api-service-request-timeout=4s").ApiServiceRequestTimeout.Should().Be(TimeSpan.FromSeconds(4));
        }

        [Fact(DisplayName =
            "Kubernetes settings should not allow server request timeout greater than operation timeout")]
        public void InvalidServerRequestTimeout()
        {
            Assert.Throws<ConfigurationException>(() =>
            {
                Conf(@"
                    lease-operation-timeout=5s
                    api-service-request-timeout=6s");
            }).Message.Should().Be("'api-service-request-timeout can not be less than 'lease-operation-timeout'");

        }
    }
}