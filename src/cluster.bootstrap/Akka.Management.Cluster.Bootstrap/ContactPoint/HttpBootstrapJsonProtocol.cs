//-----------------------------------------------------------------------
// <copyright file="HttpBootstrapJsonProtocol.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka.Actor;
using Akka.Cluster;

namespace Akka.Management.Cluster.Bootstrap.ContactPoint
{
    public class HttpBootstrapJsonProtocol
    {
        public sealed class SeedNode
        {
            public SeedNode(Address address)
            {
                Address = address;
            }

            public Address Address { get; }
        }
        
        public sealed class ClusterMember
        {
            public ClusterMember(Address node, long nodeUid, MemberStatus status, ImmutableHashSet<string> roles)
            {
                Node = node;
                NodeUid = nodeUid;
                Status = status;
                Roles = roles;
            }

            public Address Node { get; }
            public long NodeUid { get; }
            public MemberStatus Status { get; }
            public ImmutableHashSet<string> Roles { get; }
        }
        
        public sealed class SeedNodes
        {
            public SeedNodes(Address selfNode, ImmutableHashSet<ClusterMember> nodes)
            {
                SelfNode = selfNode;
                Nodes = nodes;
            }

            public Address SelfNode { get; }
            public ImmutableHashSet<ClusterMember> Nodes { get; }
        }
    }
}