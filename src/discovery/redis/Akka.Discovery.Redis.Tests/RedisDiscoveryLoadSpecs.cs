// -----------------------------------------------------------------------
//  <copyright file="RedisDiscoveryLoadSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    /// <summary>
    /// Guards the assembly-qualified <c>class</c> string in reference.conf and the embedded
    /// reference.conf resource name against rename regressions: if either breaks, loading the
    /// "redis" discovery method fails or resolves to the wrong type.
    /// </summary>
    public class RedisDiscoveryLoadSpecs
    {
        [Fact]
        public async Task Should_load_RedisServiceDiscovery_from_the_redis_method()
        {
            // Note: no Redis is running here. This also proves the connection is NOT established
            // eagerly in the discovery constructor (the guardian connects lazily).
            var config = ConfigurationFactory.ParseString(@"
                    akka.discovery.method = redis
                    akka.discovery.redis.connection-string = ""localhost:6379""
                ")
                .WithFallback(RedisDiscovery.DefaultConfiguration());

            using var system = ActorSystem.Create("redis-load-spec", config);

            var discovery = Discovery.Get(system).LoadServiceDiscovery("redis");
            Assert.IsType<RedisServiceDiscovery>(discovery);

            await system.Terminate();
        }
    }
}
