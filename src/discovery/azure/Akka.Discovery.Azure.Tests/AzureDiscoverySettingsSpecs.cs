// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoverySettingsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Azure.Identity;
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
            var settings = AzureDiscoverySettings.Create(AzureDiscovery.DefaultConfiguration());

            var assemblyName = typeof(AzureServiceDiscovery).Assembly.FullName!.Split(',')[0].Trim();
            var config = AzureDiscovery.DefaultConfiguration().GetConfig(AzureServiceDiscovery.DefaultConfigPath);
            config.GetString("class").Should().Be($"{typeof(AzureServiceDiscovery).Namespace}.{nameof(AzureServiceDiscovery)}, {assemblyName}");

            settings.ReadOnly.Should().BeFalse();
            settings.ServiceName.Should().Be("default");
            settings.HostName.Should().Be(Dns.GetHostName());
            settings.Port.Should().Be(8558);
            settings.ConnectionString.Should().Be("<connection-string>");
            settings.TableName.Should().Be("akkaclustermembers");
            settings.TtlHeartbeatInterval.Should().Be(1.Minutes());
            settings.StaleTtlThreshold.Should().Be(TimeSpan.Zero);
            settings.PruneInterval.Should().Be(1.Hours());
            settings.OperationTimeout.Should().Be(10.Seconds());
            settings.EffectiveStaleTtlThreshold.Should().Be(new TimeSpan(settings.TtlHeartbeatInterval.Ticks * 5));
            settings.AzureTableEndpoint.Should().BeNull();
            settings.AzureAzureCredential.Should().BeNull();
        }

        [Fact(DisplayName = "Empty settings variable and default settings should match")]
        public void EmptySettingsTest()
        {
            var settings = AzureDiscoverySettings.Create(AzureDiscovery.DefaultConfiguration());
            var empty = AzureDiscoverySettings.Empty;

            empty.ReadOnly.Should().Be(settings.ReadOnly);
            empty.ServiceName.Should().Be(settings.ServiceName);
            empty.HostName.Should().Be(settings.HostName);
            empty.Port.Should().Be(settings.Port);
            empty.ConnectionString.Should().Be(settings.ConnectionString);
            empty.TableName.Should().Be(settings.TableName);
            empty.TtlHeartbeatInterval.Should().Be(settings.TtlHeartbeatInterval);
            empty.StaleTtlThreshold.Should().Be(settings.StaleTtlThreshold);
            empty.PruneInterval.Should().Be(settings.PruneInterval);
            empty.OperationTimeout.Should().Be(settings.OperationTimeout);
            empty.EffectiveStaleTtlThreshold.Should().Be(settings.EffectiveStaleTtlThreshold);
            settings.AzureTableEndpoint.Should().Be(settings.AzureTableEndpoint);
            settings.AzureAzureCredential.Should().Be(settings.AzureAzureCredential);
        }

        [Fact(DisplayName = "Settings override should work properly")]
        public void SettingsWithOverrideTest()
        {
            var uri = new Uri("https://whatever.com");
            var credential = new DefaultAzureCredential();
            var settings = AzureDiscoverySettings.Empty
                .WithReadOnlyMode(true)
                .WithServiceName("a")
                .WithPublicHostName("host")
                .WithPublicPort(1234)
                .WithConnectionString("b")
                .WithTableName("c")
                .WithTtlHeartbeatInterval(1.Seconds())
                .WithStaleTtlThreshold(2.Seconds())
                .WithPruneInterval(3.Seconds())
                .WithOperationTimeout(4.Seconds())
                .WithAzureCredential(uri, credential);

            settings.ReadOnly.Should().BeTrue();
            settings.ServiceName.Should().Be("a");
            settings.HostName.Should().Be("host");
            settings.Port.Should().Be(1234);
            settings.ConnectionString.Should().Be("b");
            settings.TableName.Should().Be("c");
            settings.TtlHeartbeatInterval.Should().Be(1.Seconds());
            settings.StaleTtlThreshold.Should().Be(2.Seconds());
            settings.PruneInterval.Should().Be(3.Seconds());
            settings.OperationTimeout.Should().Be(4.Seconds());
            settings.EffectiveStaleTtlThreshold.Should().Be(settings.StaleTtlThreshold);
            settings.AzureTableEndpoint.Should().Be(uri);
            settings.AzureAzureCredential.Should().Be(credential);
        }

        [Fact(DisplayName = "Setup override should work properly")]
        public void SettingsWithSetupOverrideTest()
        {
            var uri = new Uri("https://whatever.com");
            var credential = new DefaultAzureCredential();
            var setup = new AzureDiscoverySetup()
                .WithReadOnlyMode(true)
                .WithServiceName("a")
                .WithPublicHostName("host")
                .WithPublicPort(1234)
                .WithConnectionString("b")
                .WithTableName("c")
                .WithTtlHeartbeatInterval(1.Seconds())
                .WithStaleTtlThreshold(2.Seconds())
                .WithPruneInterval(3.Seconds())
                .WithOperationTimeout(4.Seconds())
                .WithAzureCredential(uri, credential);
            
            var settings = setup.Apply(AzureDiscoverySettings.Empty);

            settings.ReadOnly.Should().BeTrue();
            settings.ServiceName.Should().Be("a");
            settings.HostName.Should().Be("host");
            settings.Port.Should().Be(1234);
            settings.ConnectionString.Should().Be("b");
            settings.TableName.Should().Be("c");
            settings.TtlHeartbeatInterval.Should().Be(1.Seconds());
            settings.StaleTtlThreshold.Should().Be(2.Seconds());
            settings.PruneInterval.Should().Be(3.Seconds());
            settings.OperationTimeout.Should().Be(4.Seconds());
            settings.EffectiveStaleTtlThreshold.Should().Be(settings.StaleTtlThreshold);
            settings.AzureTableEndpoint.Should().Be(uri);
            settings.AzureAzureCredential.Should().Be(credential);
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
            
            Invoking(() => settings.WithPublicHostName(""))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must not be empty or whitespace*");
            
            Invoking(() => settings.WithPublicPort(0))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero and less than or equal to 65535*");
            
            Invoking(() => settings.WithPublicPort(65536))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero and less than or equal to 65535*");
            
            Invoking(() => settings.WithRetryBackoff(TimeSpan.Zero, TimeSpan.FromSeconds(1)))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than zero*");
            
            Invoking(() => settings.WithRetryBackoff(TimeSpan.FromSeconds(1), TimeSpan.Zero))
                .Should().ThrowExactly<ArgumentException>().WithMessage("Must be greater than retryBackoff*");
        }
    }
}