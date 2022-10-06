// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Docker.Cluster.Splitter.Tool;
using Docker.DotNet;

if (args.Length < 3 || args[0].ToLowerInvariant() == "-h" || args[0].ToLowerInvariant() == "--help")
{
    PrintHelp();
    return 0;
}

var clusterName = args[0];
var networkName = args[1];
using var client = new DockerClientConfiguration().CreateClient();

var nodes = await client.GetNodeSetAsync(clusterName, networkName);
if (nodes.Count == 0)
{
    Console.WriteLine($"Could not find any containers inside {networkName} network that matches the pattern '{clusterName}-<int>'");
    PrintHelp();
    return -1;
}

Console.WriteLine($"{nodes.Count} nodes matching pattern '{clusterName}-<int>' found inside network {networkName}:");
foreach (var node in nodes)
{
    Console.WriteLine(node);
}

var parser = new Parser(args[2].Split(' '), $"{clusterName}-", nodes);
ImmutableHashSet<Command> commands;
try
{
    commands = parser.Parse();
}
catch (Exception e)
{
    Console.Error.WriteLine(e.Message);
    return -1;
}

foreach (var command in commands)
{
    Console.WriteLine(command);
}

var execIds = await client.CreateExecsAsync(commands, nodes);
await client.ExecuteAsync(execIds);

return 0;

#region Helpers

void PrintHelp()
{
    Console.WriteLine(@"
ClusterSplitter - Induce a split-brain condition in a cluster created using docker-compose 

SYNOPSIS
ClusterSplitter CLUSTER NETWORK COMMAND

DESCRIPTION
Create a split-brain condition in the CLUSTER docker cluster inside the NETWORK docker network.

This tool assumes that the docker container names would be in the form of ""{CLUSTER}-{NUMBER}""

COMMAND is a string in the form of

<int> [FROM <int> [<int>]...] [AND <int> [FROM] <int> [<int>]...]...

Where <int> is the docker container name NUMBER in the ""{CLUSTER}-{NUMBER}"" pattern.
If FROM is not omitted, then the listed node(s) will be split from the list of nodes in the FROM section.
If FROM is omitted, then the listed node(s) will be split from the rest of the nodes in the cluster.

NOTE:
The cluster service WILL NEED to have NET_ADMIN capability enabled inside the docker-compose.yaml file.
Example docker-compose.yaml file:

version: '3'

services:
  cluster:
    image: azure.stresstest:0.2.4
    depends_on: 
      - azurite
    cap_add:
      - NET_ADMIN");
}

#endregion
