using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Cluster;
using Akka.Http.Dsl;
using Akka.Http.Dsl.Model;
using Akka.Http.Dsl.Server;
using Akka.IO;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using static Akka.Management.Cluster.Bootstrap.ContactPoint.HttpBootstrapJsonProtocol;
using HttpResponse = Akka.Http.Dsl.Model.HttpResponse;

namespace Akka.Management.Cluster.Bootstrap.ContactPoint
{
    public class HttpClusterBootstrapRoutes
    {
        private readonly ClusterBootstrapSettings _settings;

        public HttpClusterBootstrapRoutes(ClusterBootstrapSettings settings)
        {
            _settings = settings;
        }

        public Route[] Routes
        {
            get
            {
                return new Route[]{async context =>
                {
                    if (context.Request.Method == HttpMethods.Get && context.Request.Path == "/bootstrap/seed-nodes")
                    {
                        return await GetSeedNodes()(context);
                    }

                    return null;
                }};
            }
        }

        private Route GetSeedNodes()
        {
            return context =>
            {
                var cluster = Akka.Cluster.Cluster.Get(context.ActorSystem);

                ClusterMember MemberToClusterMember(Member m)
                    => new ClusterMember(m.UniqueAddress.Address, m.UniqueAddress.Uid, m.Status, m.Roles);

                var state = cluster.State;

                // TODO shuffle the members so in a big deployment nodes start joining different ones and not all the same?
                var members = state.Members
                    .Where(m => !state.Unreachable.Contains(m))
                    .Where(m => m.Status == MemberStatus.Up ||
                                m.Status == MemberStatus.WeaklyUp ||
                                m.Status == MemberStatus.Joining)
                    .Take(_settings.ContactPoint.MaxSeedNodesToExpose)
                    .Select(MemberToClusterMember).ToImmutableHashSet();

                var json = JsonConvert.SerializeObject(
                    new SeedNodes(cluster.SelfMember.UniqueAddress.Address, members));

                return Task.FromResult((RouteResult.IRouteResult) new RouteResult.Complete(HttpResponse.Create(
                    entity: new ResponseEntity(ContentTypes.ApplicationJson, ByteString.FromString(json)))));
            };
        }
        
    }

    public static class ClusterBootstrapRequests
    {
        public static Uri BootstrapSeedNodes(Uri baseUri)
        {
            return new Uri(baseUri + "/bootstrap/seed-nodes");
        }
    }
}