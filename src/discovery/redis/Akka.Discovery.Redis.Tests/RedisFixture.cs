// -----------------------------------------------------------------------
//  <copyright file="RedisFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

#nullable enable
using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Akka.Discovery.Redis.Tests
{
    [CollectionDefinition(nameof(RedisSpecs))]
    public class RedisSpecs : ICollectionFixture<RedisFixture>
    {
    }

    /// <summary>
    /// Shared Redis Testcontainer for the integration specs. If Docker is not available the fixture
    /// records the failure instead of throwing, so specs can skip gracefully via
    /// <see cref="EnsureAvailable"/> rather than failing the whole suite on Docker-less machines.
    /// </summary>
    public class RedisFixture : IAsyncLifetime
    {
        private RedisContainer? _container;
        private string? _skipReason;

        public bool Available => _container is not null && _skipReason is null;

        public string ConnectionString
        {
            get
            {
                if (_container is null || _skipReason is not null)
                    throw new InvalidOperationException("Redis container is not available");
                return _container.GetConnectionString();
            }
        }

        /// <summary>
        /// Skips the calling test (xunit.v3 dynamic skip) when Docker/Redis is unavailable.
        /// </summary>
        public void EnsureAvailable()
        {
            if (!Available)
                Assert.Skip(_skipReason ?? "Redis Testcontainer is not available");
        }

        public async ValueTask InitializeAsync()
        {
            try
            {
                _container = new RedisBuilder()
                    .WithImage("redis:7.4")
                    .Build();
                await _container.StartAsync();
            }
            catch (Exception e)
            {
                _skipReason = $"Could not start Redis Testcontainer (Docker unavailable?): {e.Message}";
            }
        }

        /// <summary>
        /// Flushes all keys so each spec starts from a clean database.
        /// </summary>
        public async Task ClearAsync()
        {
            if (!Available)
                return;

            await using var connection = await ConnectionMultiplexer.ConnectAsync(BuildAdminOptions());
            foreach (var endpoint in connection.GetEndPoints())
            {
                var server = connection.GetServer(endpoint);
                if (!server.IsReplica)
                    await server.FlushAllDatabasesAsync();
            }
        }

        private ConfigurationOptions BuildAdminOptions()
        {
            var options = ConfigurationOptions.Parse(ConnectionString);
            options.AllowAdmin = true;
            options.AbortOnConnectFail = false;
            return options;
        }

        public async ValueTask DisposeAsync()
        {
            if (_container is not null)
                await _container.DisposeAsync();
        }
    }
}
