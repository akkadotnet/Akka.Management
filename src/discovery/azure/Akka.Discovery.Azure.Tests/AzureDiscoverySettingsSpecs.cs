// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoverySettingsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using static FluentAssertions.FluentActions;

namespace Akka.Discovery.Azure.Tests
{
    public class AzureDiscoverySettingsSpecs
    {
        [Fact(DisplayName = "Default settings should contain default values")]
        public void DefaultSettingsTest()
        {
            var config = AzureServiceDiscovery.DefaultConfig.GetConfig("akka.discovery.azure");
            var settings = AzureDiscoverySettings.Create(config);

            var assemblyName = typeof(AzureServiceDiscovery).Assembly.FullName.Split(',')[0].Trim();
            config.GetString("class").Should().Be($"{typeof(AzureServiceDiscovery).Namespace}.{nameof(AzureServiceDiscovery)}, {assemblyName}");
            
            settings.ServiceName.Should().Be("default");
            settings.ConnectionString.Should().Be("<connection-string>");
            settings.TableName.Should().Be("akka-discovery-cluster-member");
            settings.TtlHeartbeatInterval.Should().Be(1.Minutes());
            settings.StaleTtlThreshold.Should().Be(TimeSpan.Zero);
            settings.PruneInterval.Should().Be(1.Hours());
        }

        [Fact(DisplayName = "Empty settings variable and default settings should match")]
        public void EmptySettingsTest()
        {
            var config = AzureServiceDiscovery.DefaultConfig.GetConfig("akka.discovery.azure");
            var settings = AzureDiscoverySettings.Create(config);
            var empty = AzureDiscoverySettings.Empty;

            empty.ServiceName.Should().Be(settings.ServiceName);
            empty.ConnectionString.Should().Be(settings.ConnectionString);
            empty.TableName.Should().Be(settings.TableName);
            empty.TtlHeartbeatInterval.Should().Be(settings.TtlHeartbeatInterval);
            empty.StaleTtlThreshold.Should().Be(settings.StaleTtlThreshold);
            empty.PruneInterval.Should().Be(settings.PruneInterval);
        }

        [Fact(DisplayName = "Settings override should work properly")]
        public void SettingsWithOverrideTest()
        {
            var settings = AzureDiscoverySettings.Empty
                .WithServiceName("a")
                .WithConnectionString("b")
                .WithTableName("c")
                .WithTtlHeartbeatInterval(1.Seconds())
                .WithStaleTtlThreshold(2.Seconds())
                .WithPruneInterval(3.Seconds());
            
            settings.ServiceName.Should().Be("a");
            settings.ConnectionString.Should().Be("b");
            settings.TableName.Should().Be("c");
            settings.TtlHeartbeatInterval.Should().Be(1.Seconds());
            settings.StaleTtlThreshold.Should().Be(2.Seconds());
            settings.PruneInterval.Should().Be(3.Seconds());
        }

        [Fact(DisplayName = "Settings constructor should throw on invalid values")]
        public void SettingsInvalidValuesTest()
        {
            var settings = AzureDiscoverySettings.Empty;

            Invoking(() => settings.WithTtlHeartbeatInterval(TimeSpan.Zero))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero*");

            Invoking(() => settings.WithPruneInterval(TimeSpan.Zero))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero*");

            Invoking(() => settings.WithStaleTtlThreshold(1.Seconds()))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than*");
        }
    }
}