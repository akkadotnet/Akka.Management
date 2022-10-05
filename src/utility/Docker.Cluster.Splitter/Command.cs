// -----------------------------------------------------------------------
//  <copyright file="Command.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Docker.Cluster.Splitter;

public sealed class Command
{
    public Command(ImmutableHashSet<NodeInfo> targetNodes, ImmutableHashSet<NodeInfo>? fromNodes = null)
    {
        TargetNodes = targetNodes;
        FromNodes = fromNodes ?? ImmutableHashSet<NodeInfo>.Empty;
    }

    public ImmutableHashSet<NodeInfo> TargetNodes { get; }
    public ImmutableHashSet<NodeInfo> FromNodes { get; }
}