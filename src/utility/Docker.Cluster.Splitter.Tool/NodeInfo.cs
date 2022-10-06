// -----------------------------------------------------------------------
//  <copyright file="NodeInfo.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Docker.Cluster.Splitter.Tool;

public sealed class NodeInfo
{
    public NodeInfo(string id, string name, string? address)
    {
        Id = id;
        Name = name;
        Address = address;
    }

    public string Id { get; }
    public string Name { get; }
    public string? Address { get; }

    public override string ToString()
        => $"[{Id}] {Name}:{Address}";
}