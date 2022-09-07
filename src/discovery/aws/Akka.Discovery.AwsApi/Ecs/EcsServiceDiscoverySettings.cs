// -----------------------------------------------------------------------
//  <copyright file="EcsServiceDiscoverySettings.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Amazon.ECS.Model;

namespace Akka.Discovery.AwsApi.Ecs
{
    public sealed class EcsServiceDiscoverySettings
    {
        public static EcsServiceDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config.GetConfig("akka.discovery.aws-api-ecs"));
        
        public static EcsServiceDiscoverySettings Create(Configuration.Config config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            
            var tags = new List<Tag>();
            var tagConfigs = config.GetValue("tags").GetArray().Select(value => value.ToConfig()).ToList();
            foreach (var tagValue in tagConfigs)
            {
                tags.Add(new Tag
                {
                    Key = tagValue.GetString("key"),
                    Value = tagValue.GetString("value")
                });
            }

            return new EcsServiceDiscoverySettings(
                cluster: config.GetString("cluster"),
                tags: tags.ToImmutableList());
        }
        
        public EcsServiceDiscoverySettings(string cluster, ImmutableList<Tag> tags)
        {
            Cluster = cluster;
            Tags = tags;
        }

        public string Cluster { get; }
        public ImmutableList<Tag> Tags { get; }

        public EcsServiceDiscoverySettings WithCluster(string cluster)
            => Copy(cluster: cluster);

        public EcsServiceDiscoverySettings WithTags(ImmutableList<Tag> tags)
            => Copy(tags: tags);

        public EcsServiceDiscoverySettings WithTags(IEnumerable<Tag> tags)
            => Copy(tags: tags.ToImmutableList());

        private EcsServiceDiscoverySettings Copy(string cluster = null, ImmutableList<Tag> tags = null)
            => new EcsServiceDiscoverySettings(
                cluster: cluster ?? Cluster,
                tags: tags ?? Tags);
    }
}