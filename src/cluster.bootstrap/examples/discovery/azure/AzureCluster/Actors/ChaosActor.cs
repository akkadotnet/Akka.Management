// -----------------------------------------------------------------------
//  <copyright file="ChaosActor.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Util.Internal;

namespace KubernetesCluster.Actors
{
    public sealed class ChaosActor : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new ChaosActor());
        
        public ChaosActor()
        {
            var log = Context.GetLogger();
            
            ReceiveAsync<int>(async i =>
            {
                log.Info($"Received {i}");
                switch (i)
                {
                    case 1: // graceful shutdown
                        log.Error("======== Shutting down gracefully ========");
                        await Task.Delay(100);
                        await Context.System.Terminate();
                        return;
                    case 2: // crash
                        log.Error("======== Crashing system ========");
                        await Task.Delay(100);
                        Context.System.AsInstanceOf<ExtendedActorSystem>().Abort();
                        return;
                }
            });
        }
    }
}