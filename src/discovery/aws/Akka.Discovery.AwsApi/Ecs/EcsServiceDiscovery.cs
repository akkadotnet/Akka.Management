//-----------------------------------------------------------------------
// <copyright file="EcsServiceDiscovery.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Amazon.ECS;
using Amazon.ECS.Model;
using EcsTask = Amazon.ECS.Model.Task;

namespace Akka.Discovery.AwsApi.Ecs
{
    internal sealed class EcsServiceDiscovery : ServiceDiscovery
    {
        public static readonly EcsTagComparer TagComparer = new ();
        
        private readonly EcsServiceDiscoverySettings _settings;

        private AmazonECSClient? _clientDoNotUseDirectly;

        private AmazonECSClient EcsClient
        {
            get
            {
                if (_clientDoNotUseDirectly != null)
                    return _clientDoNotUseDirectly;

                var clientConfig = new AmazonECSConfig();

                _clientDoNotUseDirectly = new AmazonECSClient(clientConfig);
                return _clientDoNotUseDirectly;
            }
        }
        
        public EcsServiceDiscovery(ActorSystem system)
        {
            _settings = AwsEcsDiscovery.Get(system).Settings;
        }
        
        public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
        {
            using var cts = new CancellationTokenSource(resolveTimeout);
            var tasks = await ResolveTasks(
                ecsClient: EcsClient,
                cluster: _settings.Cluster,
                serviceName: lookup.ServiceName,
                tags: _settings.Tags,
                token: cts.Token);

            var addresses = new List<ResolvedTarget>();
            foreach (var task in tasks)
            {
                foreach (var container in task.Containers)
                {
                    foreach (var networkInterface in container.NetworkInterfaces)
                    {
                        var address = networkInterface.PrivateIpv4Address;
                        var parsed = IPAddress.TryParse(address, out var ip) ;
                        addresses.Add(new ResolvedTarget(address, null, parsed ? ip : null));
                    }
                }
            }
            return new Resolved(lookup.ServiceName, addresses);
        }

        private static async Task<List<EcsTask>> ResolveTasks(
            AmazonECSClient ecsClient,
            string cluster,
            string serviceName,
            ImmutableList<Tag> tags,
            CancellationToken token)
        {
            var taskArns = await ListTaskArns(ecsClient, cluster, serviceName, token);
            var tasks = await DescribeTasks(ecsClient, cluster, taskArns, token);
            // only return tasks with the exact same tags as the filter
            var tasksWithTags = tasks.Where(task => task.Tags.IsSame(tags, TagComparer)).ToList();
            return tasksWithTags;
        }

        private static async Task<List<string>> ListTaskArns(
            AmazonECSClient ecsClient,
            string cluster,
            string serviceName,
            CancellationToken token)
        {
            var listTaskRequest = new ListTasksRequest
            {
                Cluster = cluster,
                ServiceName = serviceName,
                DesiredStatus = DesiredStatus.RUNNING
            };
            
            var accumulator = new List<string>();
            do
            {
                var listTaskResult = await ecsClient.ListTasksAsync(listTaskRequest, token);
                if (token.IsCancellationRequested)
                    break;
                accumulator.AddRange(listTaskResult.TaskArns);
                listTaskRequest.NextToken = listTaskResult.NextToken;
            } while (listTaskRequest.NextToken != null);

            return accumulator;
        }

        private static async Task<List<EcsTask>> DescribeTasks(
            AmazonECSClient ecsClient,
            string cluster,
            List<string> taskArns,
            CancellationToken token)
        {
            var include = new List<string> {TaskField.TAGS};
            var accumulator = new List<EcsTask>();
            // split batch into chunks of 100 requests
            var lists = taskArns.ChunkBy(100); 
            foreach (var arns in lists)
            {
                var response = await ecsClient.DescribeTasksAsync(new DescribeTasksRequest
                {
                    Cluster = cluster, 
                    Tasks = arns.ToList(),
                    Include = include
                }, token);
                if (token.IsCancellationRequested)
                    break;
                
                accumulator.AddRange(response.Tasks);
            }

            return accumulator;
        }

    }
}