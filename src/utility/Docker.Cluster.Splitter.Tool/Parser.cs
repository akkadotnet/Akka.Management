// -----------------------------------------------------------------------
//  <copyright file="Parser.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Docker.Cluster.Splitter.Tool;

public class Parser
{
    private const string From = "FROM";
    private const string And = "AND";
    
    private readonly string[] _args;
    private readonly string _prefix;
    private readonly ImmutableHashSet<NodeInfo> _infos;

    public Parser(string[] args, string prefix, ImmutableHashSet<NodeInfo> infos)
    {
        _args = args;
        _prefix = prefix;
        _infos = infos;
    }

    public ImmutableHashSet<Command> Parse()
    {
        var result = new HashSet<Command>();
        var state = ParseState.NumTarget;
        var index = 0;

        var targets = new List<NodeInfo>();
        var froms = new List<NodeInfo>();
        while (true)
        {
            var arg = _args[index].ToUpperInvariant();
            switch (state)
            {
                case ParseState.NumTarget:
                    switch (arg)
                    {
                        case From:
                            if (targets.Count == 0)
                                throw new Exception($"Invalid command string at index {index}, no <int> TARGET found before FROM");
                            state = ParseState.NumFrom;
                            break;
                        case And:
                            if (targets.Count == 0)
                                throw new Exception($"Invalid command string at index {index}, no <int> TARGET found before AND");
                            result.Add(new Command(targets, froms));
                            targets.Clear();
                            froms.Clear();
                            break;
                        default:
                            if (!int.TryParse(arg, out var num))
                                throw new Exception($"Invalid command string at index {index}, can not parse '{arg}' into <int> TARGET");
                            var target = _infos.FirstOrDefault(n => n.Name == $"{_prefix}{num}");
                            if (target is null)
                                throw new Exception($"Invalid command string at index {index}, can not find node {_prefix}{num}");
                            targets.Add(target);
                            break;
                    }
                    break;
                case ParseState.NumFrom:
                    switch (arg)
                    {
                        case From:
                            throw new Exception($"Invalid command string at index {index}, FROM FROM is invalid");
                        
                        case And:
                            result.Add(new Command(targets, froms));
                            targets.Clear();
                            froms.Clear();
                            state = ParseState.NumTarget;
                            break;
                        
                        default:
                            if (!int.TryParse(arg, out var num))
                                throw new Exception($"Invalid command string at index {index}, can not parse {arg} into <int> FROM");
                            var from = _infos.FirstOrDefault(n => n.Name == $"{_prefix}{num}");
                            if (from is null)
                                throw new Exception($"Invalid command string at index {index}, can not find node {_prefix}{num}");
                            froms.Add(from);
                            break;
                    }
                    break;
            }
            
            index++;
            if (index == _args.Length)
                break;
        }

        if (targets.Count > 0)
            result.Add(new Command(targets, froms));
        
        return result.ToImmutableHashSet();
    }
}