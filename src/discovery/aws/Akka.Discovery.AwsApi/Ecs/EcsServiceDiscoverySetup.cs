// -----------------------------------------------------------------------
//  <copyright file="EcsServiceDiscoverySetup.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Akka.Actor.Setup;
using Amazon.ECS.Model;

namespace Akka.Discovery.AwsApi.Ecs
{
    public class EcsServiceDiscoverySetup: Setup
    {
        public string? Cluster { get; set; }
        public IEnumerable<Tag>? Tags { get; set; }

        internal EcsServiceDiscoverySettings Apply(EcsServiceDiscoverySettings settings)
        {
            if (Cluster != null)
                settings = settings.WithCluster(Cluster);
            if (Tags != null)
                settings = settings.WithTags(Tags);
            return settings;
        }
    }
}