//-----------------------------------------------------------------------
// <copyright file="HttpContactPointRoutesSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using Akka.Cluster;
using Akka.Configuration;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.Http.Extensions;
using Akka.Management.Cluster.Bootstrap.ContactPoint;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using HttpRequest = Akka.Http.Dsl.Model.HttpRequest;
using static Akka.Management.Cluster.Bootstrap.ContactPoint.HttpBootstrapJsonProtocol;

namespace Akka.Management.Cluster.Bootstrap.Tests.ContactPoint
{
    public class HttpContactPointRoutesSpec : TestKit.Xunit2.TestKit
    {
        private static readonly Config Config = ConfigurationFactory.ParseString(@"
            akka.actor.provider = cluster
            akka.remote.dot-netty.tcp.hostname = ""127.0.0.1""
            akka.remote.dot-netty.tcp.port = 0")
            .WithFallback(ClusterBootstrap.DefaultConfiguration())
            .WithFallback(AkkaManagementProvider.DefaultConfiguration());

        private readonly ClusterBootstrapSettings _settings;
        private readonly HttpClusterBootstrapRoutes _httpBootstrap;

        public HttpContactPointRoutesSpec(ITestOutputHelper helper) 
            : base(Config, nameof(HttpContactPointRoutesSpec), helper)
        {
            _settings = ClusterBootstrapSettings.Create(Sys.Settings.Config, Sys.Log);
            _httpBootstrap = new HttpClusterBootstrapRoutes(_settings);
        }

        [Fact(DisplayName = "Http Bootstrap routes should empty list if node is not part of a cluster")]
        public async Task EmptyListIfNotPartOfCluster()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = ClusterBootstrapRequests.BootstrapSeedNodes("").ToString();
            
            var requestContext = new RequestContext(await HttpRequest.CreateAsync(context.Request), Sys);
            var response = (RouteResult.Complete) await _httpBootstrap.Routes.Concat()(requestContext);
            response.Response.Entity.DataBytes.ToString().Should().Contain("\"Nodes\":[]");
        }

        [Fact( 
            Skip = "Extremely racy in CI/CD",
            DisplayName = "Http Bootstrap routes should include seed nodes when part of a cluster")]
        public async Task IncludeSeedsWhenPartOfCluster()
        {
            var cluster = Akka.Cluster.Cluster.Get(Sys);
            cluster.Join(cluster.SelfAddress);

            var p = CreateTestProbe();
            cluster.Subscribe(
                p.Ref,
                ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents,
                typeof(ClusterEvent.MemberUp));

            var up = p.ExpectMsg<ClusterEvent.MemberUp>();
            up.Member.Should().Be(cluster.SelfMember);

            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = ClusterBootstrapRequests.BootstrapSeedNodes("");
            
            var requestContext = new RequestContext(await HttpRequest.CreateAsync(context.Request), Sys);
            var response = (RouteResult.Complete) await _httpBootstrap.Routes.Concat()(requestContext);

            var responseString = response.Response.Entity.DataBytes.ToString();
            var nodes = JsonConvert.DeserializeObject<SeedNodes>(responseString);
            
            var seedNodes = nodes.Nodes.Select(n => n.Node).ToList();
            seedNodes.Contains(cluster.SelfAddress).Should()
                .BeTrue(
                    "Seed nodes should contain self address but it does not. Self address: [{0}], seed nodes: [{1}], response string: [{2}]",
                    cluster.SelfAddress,
                    string.Join(", ", seedNodes),
                    responseString);
        }
    }
}