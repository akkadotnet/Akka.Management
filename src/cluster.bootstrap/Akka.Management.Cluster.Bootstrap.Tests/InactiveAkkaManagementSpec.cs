// -----------------------------------------------------------------------
// <copyright file="InactiveAkkaManagementSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Event;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Debug = System.Diagnostics.Debug;

namespace Akka.Management.Cluster.Bootstrap.Tests
{
    public class InactiveAkkaManagementSpec : TestKit.Xunit2.TestKit
    {
        private static readonly Config Config = ConfigurationFactory
            .ParseString(@"
akka.loglevel = DEBUG
akka.actor.provider = cluster
akka.discovery.method = config
akka.remote.dot-netty.tcp.port = 0
")
            .WithFallback(DiscoveryProvider.DefaultConfiguration());
        
        public InactiveAkkaManagementSpec(ITestOutputHelper output)
            : base(Config, nameof(InactiveAkkaManagementSpec), output)
        {
            
        }

        [Fact(DisplayName = "ClusterBootstrap should log an error after 10 seconds if AkkaManagement is not run")]
        public async Task ShouldLogErrorIfAkkaManagementIsNotRunning()
        {
            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe.Ref, typeof(Error));
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
            var exceptionTask = Record.ExceptionAsync(async () =>
            {
                await ClusterBootstrap.Get(Sys).Start();
            });

            var finishedTask = await Task.WhenAny(timeoutTask, exceptionTask);
            finishedTask.Should().NotBe(timeoutTask, "ClusterBootstrap failed to stop itself after 10 seconds");
            exceptionTask.IsCompletedSuccessfully.Should().BeTrue();
            
            var exception = exceptionTask.Result;
            exception.Should().NotBeNull();
            // ReSharper disable once PossibleNullReferenceException
            exception.Message.Should().Be("Awaiting ClusterBootstrap.SelfContactPointUri timed out.");
            await AwaitAssertAsync(() =>
            {
                probe.ExpectMsg<Error>().Message.ToString().Should()
                    .StartWith("'Bootstrap.selfContactPoint' was NOT set, but is required for the bootstrap to work");
            });
        }
    }
}