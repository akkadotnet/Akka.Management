//-----------------------------------------------------------------------
// <copyright file="HealthCheckSettingsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using FluentAssertions;
using Xunit;

namespace Akka.Management.Tests
{
    public class HealthCheckSettingsSpec
    {
        [Fact]
        public void SettingsShouldContainDefaultValues()
        {
            var settings = HealthCheckSettings.Create(
                AkkaManagementProvider.DefaultConfiguration().GetConfig("akka.management.health-checks"));

            settings.CheckTimeout.Should().Be(TimeSpan.FromSeconds(1));
            settings.LivenessChecks.IsEmpty.Should().BeTrue();
            settings.LivenessPath.Should().Be("/alive");
            settings.ReadinessChecks.IsEmpty.Should().BeTrue();
            settings.ReadinessPath.Should().Be("/ready");
        }

        [Fact]
        public void SettingsShouldFilterOutBlankFqcn()
        {
            HealthCheckSettings.Create(ConfigurationFactory.ParseString(@"
                readiness-checks {
                    cluster-membership = """"
                }
                liveness-checks {
                }
                readiness-path = """"
                liveness-path = """"
                check-timeout = 1s
            ")).ReadinessChecks.IsEmpty.Should().BeTrue();
            
            HealthCheckSettings.Create(ConfigurationFactory.ParseString(@"
                readiness-checks {
                }
                liveness-checks {
                    cluster-membership = """"
                }
                readiness-path = """"
                liveness-path = """"
                check-timeout = 1s
            ")).LivenessChecks.IsEmpty.Should().BeTrue();
        }
    }
}