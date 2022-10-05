// -----------------------------------------------------------------------
//  <copyright file="Parser.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Docker.Cluster.Splitter;

public class Parser
{
    private string[] _args;
    private ImmutableHashSet<NodeInfo> _infos;

    public Parser(string[] args, ImmutableHashSet<NodeInfo> infos)
    {
        _args = args;
        _infos = infos;
    }

    public ImmutableHashSet<Command> Parse()
    {
        var result = new HashSet<Command>();
        var state = ParseState.Num;

        // TODO Implement this
        
        return result.ToImmutableHashSet();
    }
}