// -----------------------------------------------------------------------
//  <copyright file="DockerTasks.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Docker.Cluster.Splitter.Tool;

public static class DockerExtensions
{
    public static async Task<ImmutableHashSet<NodeInfo>> GetNodeSetAsync(
        this DockerClient client, 
        string clusterName,
        string networkName)
    {
        var networkResponse = await client.Networks.InspectNetworkAsync(networkName);
        var result = networkResponse.Containers.Select(kvp =>
        {
            var rawIp = string.IsNullOrEmpty(kvp.Value.IPv4Address) ? kvp.Value.IPv6Address : kvp.Value.IPv4Address;
            string? ip = null;
            if(!string.IsNullOrEmpty(rawIp))
            {
                var parts = rawIp.Split("/");
                ip = parts[0];
            }
            
            return new NodeInfo(
                id: kvp.Key,
                name: kvp.Value.Name,
                address: ip);
        });
        return result.Where(node => !string.IsNullOrEmpty(node.Address) && node.Name.StartsWith($"{clusterName}-")).ToImmutableHashSet();
    }

    public static async Task<ImmutableList<string>> CreateExecsAsync(
        this DockerClient client,
        ImmutableHashSet<Command> commands,
        ImmutableHashSet<NodeInfo> infos)
    {
        var execIds = new List<string>();
        
        foreach (var command in commands)
        {
            var targets = command.TargetNodes;
            var froms = command.FromNodes;

            #region Validation

            if (targets.Count == 0)
                throw new Exception("TARGET nodes can not be empty");

            var notFound = targets.Except(infos);
            if (notFound.Count > 0)
                throw new Exception(
                    $"Could not find these TARGET nodes inside the cluster: {string.Join(",", notFound.Select(n => n.Name))}");

            if (targets.Count == infos.Count)
                throw new Exception("TARGET nodes must be a subset of the whole cluster");
            
            if (froms.Count > 0)
            {
                notFound = froms.Except(infos);
                if (notFound.Count > 0)
                    throw new Exception(
                        $"Could not find these FROM nodes inside the cluster: {string.Join(",", notFound.Select(n => n.Name))}");

                var invalid = froms.Intersect(targets);
                if (invalid.Count > 0)
                    throw new Exception(
                        $"TARGET and FROM could not overlap each other. Invalid nodes: {string.Join(",", invalid.Select(n => n.Name))}");
            }

            #endregion

            if (froms.Count == 0)
                froms = infos.Except(targets);

            foreach (var target in targets)
            {
                var cmd = string.Join(" && ", froms.Select(n => $"ip route add prohibit {n.Address}"));
                var response = await client.Exec.ExecCreateContainerAsync(target.Id, new ContainerExecCreateParameters
                {
                    AttachStderr = true,
                    AttachStdout = true,
                    AttachStdin = true,
                    Cmd = new[] { "/bin/bash", "-c", cmd}
                });
                
                execIds.Add(response.ID);
            }
        }

        return execIds.ToImmutableList();
    }

    public static async Task ExecuteAsync(this DockerClient client, ImmutableList<string> execIds)
    {
        using var cts = new CancellationTokenSource(5000);
        await using var outStream = Console.OpenStandardOutput();
        await using var errStream = Console.OpenStandardError();
        var tasks = new List<Task>();
        foreach (var execId in execIds)
        {
            var stream = await client.Exec.StartAndAttachContainerExecAsync(execId, false, cts.Token);
            tasks.Add(stream.CopyOutputToAsync(outStream, outStream, errStream, cts.Token));
        }
        
        await Task.WhenAll(tasks);
    }
}