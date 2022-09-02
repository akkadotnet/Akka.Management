//-----------------------------------------------------------------------
// <copyright file="EcsServiceDiscovery.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2021 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2021 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Util;
using Amazon;
using Amazon.ECS;
using Amazon.ECS.Model;
using EcsTask = Amazon.ECS.Model.Task;

namespace Akka.Discovery.AwsApi.Ecs
{
    internal sealed class EcsServiceDiscovery : ServiceDiscovery
    {
        private static Either<string, IPAddress> ContainerAddress
        {
            get
            {
                var hostName = Dns.GetHostName();
                var ipHostEntries = Dns.GetHostEntry(hostName);
                var ipEntries = ipHostEntries.AddressList
                    .Where(ip => ip.IsSiteLocalAddress() && !ip.IsLoopbackAddress()).ToList();
                if (ipEntries.Count == 0)
                    return new Right<string, IPAddress>(ipEntries[0]);
                return new Left<string, IPAddress>(
                    $"Exactly one private address must be configured (found: [{string.Join(",", ipEntries)}])");            
            }
        }

        private readonly Configuration.Config _config;
        private readonly string _cluster;
        private readonly List<Tag> _tags = new List<Tag>();

        private AmazonECSClient _clientDoNotUseDirectly;

        private AmazonECSClient EcsClient
        {
            get
            {
                if (_clientDoNotUseDirectly != null)
                    return _clientDoNotUseDirectly;

                var clientConfig = new AmazonECSConfig();
                if (_config.HasPath("endpoint"))
                {
                    var endpoint = _config.GetString("endpoint");
                    clientConfig.ServiceURL = endpoint;
                }

                if (_config.HasPath("region"))
                {
                    var region = _config.GetString("region");
                    clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
                }

                _clientDoNotUseDirectly = new AmazonECSClient(clientConfig);
                return _clientDoNotUseDirectly;
            }
        }
        
        public EcsServiceDiscovery(ActorSystem system)
        {
            _config = system.Settings.Config.GetConfig("akka.discovery.aws-api-ecs");
            _cluster = _config.GetString("cluster");
            var tags = _config.GetValue("tags").GetArray().Select(value => value.ToConfig()).ToList();
            foreach (var tagValue in tags)
            {
                _tags.Add(new Tag
                {
                    Key = tagValue.GetString("key"),
                    Value = tagValue.GetString("value")
                });
            }
        }
        
        public override async Task<Resolved> Lookup(Lookup lookup, TimeSpan resolveTimeout)
        {
            using (var cts = new CancellationTokenSource(resolveTimeout))
            {
                var tasks = await ResolveTasks(EcsClient, _cluster, lookup.ServiceName, _tags, cts.Token);

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
        }

        private static async Task<List<EcsTask>> ResolveTasks(
            AmazonECSClient ecsClient,
            string cluster,
            string serviceName,
            List<Tag> tags,
            CancellationToken token)
        {
            var taskArns = await ListTaskArns(ecsClient, cluster, serviceName, token);
            var tasks = await DescribeTasks(ecsClient, cluster, taskArns, token);
            var tasksWithTags = tasks.Where(task =>
                {
                    foreach (var tag in tags)
                    {
                        if (task.Tags.Any(t => t.Key == tag.Key && t.Value == tag.Value))
                            return false;
                    }
                    return true;
                }
            ).ToList();
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
            var lists = taskArns
                .Select((value, index) => new {value, index})
                .GroupBy(x => x.index / 100)
                .Select(x => x.Select(v => v.value).ToList());
            foreach (var arns in lists)
            {
                var response = await ecsClient.DescribeTasksAsync(new DescribeTasksRequest
                {
                    Cluster = cluster, 
                    Tasks = arns,
                    Include = include
                }, token);
                accumulator.AddRange(response.Tasks);
            }

            return accumulator;
        }
    }
}