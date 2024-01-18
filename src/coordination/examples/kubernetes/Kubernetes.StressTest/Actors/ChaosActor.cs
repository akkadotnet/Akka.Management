// -----------------------------------------------------------------------
//   <copyright file="ChaosActor.cs" company="Petabridge, LLC">
//     Copyright (C) 2015-2024 .NET Petabridge, LLC
//   </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Util.Internal;

namespace Kubernetes.StressTest.Actors;

public sealed class ChaosActor : ReceiveActor
{
    public ChaosActor()
    {
        var log = Context.GetLogger();
        ReceiveAsync<int>(async i =>
        {
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
