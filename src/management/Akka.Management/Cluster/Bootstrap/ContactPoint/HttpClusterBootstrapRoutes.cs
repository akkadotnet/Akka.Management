//-----------------------------------------------------------------------
// <copyright file="HttpClusterBootstrapRoutes.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Http;
using Akka.Http.Dsl;
using Akka.Http.Extensions;
using Ceen;
using Newtonsoft.Json;
using static Akka.Management.Cluster.Bootstrap.ContactPoint.HttpBootstrapJsonProtocol;
using Route = System.ValueTuple<string, Akka.Http.Dsl.HttpModuleBase>;

namespace Akka.Management.Cluster.Bootstrap.ContactPoint
{
    public class HttpClusterBootstrapRoutes: HttpModuleBase
    {
        private readonly ClusterBootstrapSettings _settings;

        public HttpClusterBootstrapRoutes(ClusterBootstrapSettings settings)
        {
            _settings = settings;
        }

        public Route[] Routes => new Route[] { ("/bootstrap/seed-nodes", this) };

        public override async Task<bool> HandleAsync(IAkkaHttpContext context)
        {
            if (context.HttpContext.Request.Method.ToLowerInvariant() != "get")
                return false;
            
            // Check that clustering is in effect
            if (((ExtendedActorSystem)context.ActorSystem).Provider is not IClusterActorRefProvider)
            {
                context.HttpContext.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
                var response = JsonConvert.SerializeObject(new
                {
                    error = new
                    {
                        reason = "not available",
                        message = "Clustering is not available"
                    },
                    code = (int)HttpStatusCode.ServiceUnavailable,
                    message = "Clustering is not available"
                });
                await context.HttpContext.Response.WriteAllJsonAsync(response);
                return true;
            }
            
            var cluster = Akka.Cluster.Cluster.Get(context.ActorSystem);

            if (cluster.SelfMember.Status
                is MemberStatus.Down
                or MemberStatus.Exiting
                or MemberStatus.Leaving
                or MemberStatus.Removed)
            {
                var body = JsonConvert.SerializeObject(
                    new SeedNodes(cluster.SelfMember.UniqueAddress.Address, ImmutableList<ClusterMember>.Empty));
                await context.HttpContext.Response.WriteAllJsonAsync(body);
                return true;
            }
            
            var state = cluster.State;

            var members = state.Members
                .Where(m => !state.Unreachable.Contains(m))
                .Where(m => m.Status is MemberStatus.Up or MemberStatus.WeaklyUp or MemberStatus.Joining)
                .Take(_settings.ContactPoint.MaxSeedNodesToExpose)
                .Select(MemberToClusterMember).ToList().Shuffle();

            var json = JsonConvert.SerializeObject(
                new SeedNodes(cluster.SelfMember.UniqueAddress.Address, members.ToImmutableList()));

            await context.HttpContext.Response.WriteAllJsonAsync(json);

            return true;

            ClusterMember MemberToClusterMember(Member m) =>
                new (m.UniqueAddress.Address, m.UniqueAddress.Uid, m.Status, m.Roles);
        }
    }

    public static class ClusterBootstrapRequests
    {
        public static string BootstrapSeedNodes(Uri baseUri)
            => baseUri + "bootstrap/seed-nodes";
        
        public static string BootstrapSeedNodes(string baseUri)
            => $"{baseUri}/bootstrap/seed-nodes";
    }
}