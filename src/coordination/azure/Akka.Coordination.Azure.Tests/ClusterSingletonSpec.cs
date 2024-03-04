// -----------------------------------------------------------------------
//  <copyright file="ClusterSingletonSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Event;
using Akka.Hosting;
using Akka.Remote.Hosting;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Coordination.Azure.Tests
{
    public class ClusterSingletonSpec: Hosting.TestKit.TestKit
    {
        private class EchoActor : ActorBase
        {
            protected override bool Receive(object message)
            {
                Sender.Tell(message);
                return true;
            }
        }
        
        private enum SingletonKey
        { }
        
        private const string ConnectionString = "UseDevelopmentStorage=true";
        
        public ClusterSingletonSpec(ITestOutputHelper output): base("TestCluster", output)
        {
            Util.Cleanup(ConnectionString).GetAwaiter().GetResult();
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            var options = new AzureLeaseOption
            {
                ConnectionString = ConnectionString,
                ContainerName = "akka-coordination-lease"
            };
            builder
                .WithRemoting()
                .WithClustering()
                .WithAzureLease(options)
                .WithSingleton<SingletonKey>(
                    "Echo-singleton", 
                    Props.Create(() => new EchoActor()),
                    new ClusterSingletonOptions
                    {
                        LeaseImplementation = options
                    })
                .AddStartup((system, registry) =>
                {
                    var cluster = Cluster.Cluster.Get(system);
                    cluster.Join(cluster.SelfAddress);
                });
        }

        [Fact(DisplayName = "WithAzureLease and Cluster.Singleton should work")]
        public async Task ClusterSingletonWithAzureLeaseShouldWork()
        {
            var probe = CreateTestProbe();
            Sys.EventStream.Subscribe(probe, typeof(LogEvent));
            
            var tcs = new TaskCompletionSource<Done>();
            var cluster = Cluster.Cluster.Get(Sys);
            cluster.RegisterOnMemberUp(() =>
            {
                tcs.SetResult(Done.Instance);
            });
            await tcs.Task.WaitAsync(30.Seconds());
            
            var singleton = ActorRegistry.Get<SingletonKey>();
            (await singleton.Ask("test")).Should().Be("test");

            await probe.FishForMessageAsync(evt => evt is Info i && (i.Message?.ToString()?.StartsWith("Acquire lease result") ?? false));
        }
    }
}