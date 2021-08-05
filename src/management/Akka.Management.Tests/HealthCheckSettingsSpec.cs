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