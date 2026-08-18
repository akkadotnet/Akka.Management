// -----------------------------------------------------------------------
//  <copyright file="AzureDiscoverySettingsSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net;
using Azure.Identity;
using Xunit;

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
            Assert.Equal($"{typeof(AzureServiceDiscovery).Namespace}.{nameof(AzureServiceDiscovery)}, {assemblyName}", config.GetString("class"));

            Assert.False(settings.ReadOnly);
            Assert.Equal("default", settings.ServiceName);
            Assert.Equal(Dns.GetHostName(), settings.HostName);
            Assert.Equal(8558, settings.Port);
            Assert.Equal("<connection-string>", settings.ConnectionString);
            Assert.Equal("akkaclustermembers", settings.TableName);
            Assert.Equal(TimeSpan.FromMinutes(1), settings.TtlHeartbeatInterval);
            Assert.Equal(TimeSpan.Zero, settings.StaleTtlThreshold);
            Assert.Equal(TimeSpan.FromHours(1), settings.PruneInterval);
            Assert.Equal(TimeSpan.FromSeconds(10), settings.OperationTimeout);
            Assert.Equal(new TimeSpan(settings.TtlHeartbeatInterval.Ticks * 5), settings.EffectiveStaleTtlThreshold);
            Assert.Null(settings.AzureTableEndpoint);
            Assert.Null(settings.AzureAzureCredential);
        }

        [Fact(DisplayName = "Empty settings variable and default settings should match")]
        public void EmptySettingsTest()
        {
            var settings = AzureDiscoverySettings.Create(AzureDiscovery.DefaultConfiguration());
            var empty = AzureDiscoverySettings.Empty;

            Assert.Equal(settings.ReadOnly, empty.ReadOnly);
            Assert.Equal(settings.ServiceName, empty.ServiceName);
            Assert.Equal(settings.HostName, empty.HostName);
            Assert.Equal(settings.Port, empty.Port);
            Assert.Equal(settings.ConnectionString, empty.ConnectionString);
            Assert.Equal(settings.TableName, empty.TableName);
            Assert.Equal(settings.TtlHeartbeatInterval, empty.TtlHeartbeatInterval);
            Assert.Equal(settings.StaleTtlThreshold, empty.StaleTtlThreshold);
            Assert.Equal(settings.PruneInterval, empty.PruneInterval);
            Assert.Equal(settings.OperationTimeout, empty.OperationTimeout);
            Assert.Equal(settings.EffectiveStaleTtlThreshold, empty.EffectiveStaleTtlThreshold);
            Assert.Equal(settings.AzureTableEndpoint, settings.AzureTableEndpoint);
            Assert.Equal(settings.AzureAzureCredential, settings.AzureAzureCredential);
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
                .WithTtlHeartbeatInterval(TimeSpan.FromSeconds(1))
                .WithStaleTtlThreshold(TimeSpan.FromSeconds(2))
                .WithPruneInterval(TimeSpan.FromSeconds(3))
                .WithOperationTimeout(TimeSpan.FromSeconds(4))
                .WithAzureCredential(uri, credential);

            Assert.True(settings.ReadOnly);
            Assert.Equal("a", settings.ServiceName);
            Assert.Equal("host", settings.HostName);
            Assert.Equal(1234, settings.Port);
            Assert.Equal("b", settings.ConnectionString);
            Assert.Equal("c", settings.TableName);
            Assert.Equal(TimeSpan.FromSeconds(1), settings.TtlHeartbeatInterval);
            Assert.Equal(TimeSpan.FromSeconds(2), settings.StaleTtlThreshold);
            Assert.Equal(TimeSpan.FromSeconds(3), settings.PruneInterval);
            Assert.Equal(TimeSpan.FromSeconds(4), settings.OperationTimeout);
            Assert.Equal(settings.StaleTtlThreshold, settings.EffectiveStaleTtlThreshold);
            Assert.Equal(uri, settings.AzureTableEndpoint);
            Assert.Equal(credential, settings.AzureAzureCredential);
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
                .WithTtlHeartbeatInterval(TimeSpan.FromSeconds(1))
                .WithStaleTtlThreshold(TimeSpan.FromSeconds(2))
                .WithPruneInterval(TimeSpan.FromSeconds(3))
                .WithOperationTimeout(TimeSpan.FromSeconds(4))
                .WithAzureCredential(uri, credential);
            
            var settings = setup.Apply(AzureDiscoverySettings.Empty);

            Assert.True(settings.ReadOnly);
            Assert.Equal("a", settings.ServiceName);
            Assert.Equal("host", settings.HostName);
            Assert.Equal(1234, settings.Port);
            Assert.Equal("b", settings.ConnectionString);
            Assert.Equal("c", settings.TableName);
            Assert.Equal(TimeSpan.FromSeconds(1), settings.TtlHeartbeatInterval);
            Assert.Equal(TimeSpan.FromSeconds(2), settings.StaleTtlThreshold);
            Assert.Equal(TimeSpan.FromSeconds(3), settings.PruneInterval);
            Assert.Equal(TimeSpan.FromSeconds(4), settings.OperationTimeout);
            Assert.Equal(settings.StaleTtlThreshold, settings.EffectiveStaleTtlThreshold);
            Assert.Equal(uri, settings.AzureTableEndpoint);
            Assert.Equal(credential, settings.AzureAzureCredential);
        }

        [Fact(DisplayName = "Settings constructor should throw on invalid values")]
        public void SettingsInvalidValuesTest()
        {
            var settings = AzureDiscoverySettings.Empty;

            // converted from ThrowExactly + WithMessage glob "Must be greater than zero*"
            Assert.StartsWith("Must be greater than zero",
                Assert.Throws<ArgumentException>(() => { settings.WithTtlHeartbeatInterval(TimeSpan.Zero); }).Message);

            Assert.StartsWith("Must be greater than zero",
                Assert.Throws<ArgumentException>(() => { settings.WithPruneInterval(TimeSpan.Zero); }).Message);

            Assert.StartsWith("Must be greater than",
                Assert.Throws<ArgumentException>(() => { settings.WithStaleTtlThreshold(TimeSpan.FromSeconds(1)); }).Message);

            Assert.StartsWith("Must not be empty or whitespace",
                Assert.Throws<ArgumentException>(() => { settings.WithPublicHostName(""); }).Message);

            Assert.StartsWith("Must be greater than zero and less than or equal to 65535",
                Assert.Throws<ArgumentException>(() => { settings.WithPublicPort(0); }).Message);

            Assert.StartsWith("Must be greater than zero and less than or equal to 65535",
                Assert.Throws<ArgumentException>(() => { settings.WithPublicPort(65536); }).Message);

            Assert.StartsWith("Must be greater than zero",
                Assert.Throws<ArgumentException>(() => { settings.WithRetryBackoff(TimeSpan.Zero, TimeSpan.FromSeconds(1)); }).Message);

            Assert.StartsWith("Must be greater than retryBackoff",
                Assert.Throws<ArgumentException>(() => { settings.WithRetryBackoff(TimeSpan.FromSeconds(1), TimeSpan.Zero); }).Message);
        }
    }
}