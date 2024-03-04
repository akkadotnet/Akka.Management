// -----------------------------------------------------------------------
// <copyright file="InactiveAkkaManagementSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Discovery;
using Akka.Event;
using Akka.Management.Cluster.Bootstrap;
using Akka.TestKit.Extensions;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
using static FluentAssertions.FluentActions;

namespace Akka.Management.Tests.Cluster.Bootstrap
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

            await Awaiting(() => ClusterBootstrap.Get(Sys).Start())
                .Should().ThrowAsync<Exception>()
                .WithMessage("Awaiting ClusterBootstrap.SelfContactPointUri timed out.")
                .ShouldCompleteWithin(15.Seconds(), "ClusterBootstrap failed to stop itself after 10 seconds");

            await AwaitAssertAsync(() =>
            {
                probe.ExpectMsg<Error>().Message.ToString().Should()
                    .StartWith("'Bootstrap.selfContactPoint' was NOT set, but is required for the bootstrap to work");
            });
        }
    }
}