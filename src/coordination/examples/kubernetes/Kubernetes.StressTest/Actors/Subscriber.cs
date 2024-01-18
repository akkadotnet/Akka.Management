// -----------------------------------------------------------------------
//   <copyright file="Subscriber.cs" company="Petabridge, LLC">
//     Copyright (C) 2015-2024 .NET Petabridge, LLC
//   </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Event;
using Akka.Util;

namespace Kubernetes.StressTest.Actors;

public class Subscriber : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public Subscriber()
    {
        var mediator = DistributedPubSub.Get(Context.System).Mediator;

        // subscribe to the topic named "content"
        mediator.Tell(new Subscribe("content", Self));

        Receive<int>(s =>
        {
            _log.Info($"Got {s}");
            if (s % 2 == 0)
            {
                mediator.Tell(new Publish("content", ThreadLocalRandom.Current.Next(0,10)));
            }
        });

        Receive<SubscribeAck>(subscribeAck =>
        {
            if (subscribeAck.Subscribe.Topic.Equals("content")
                && subscribeAck.Subscribe.Ref.Equals(Self)
                && subscribeAck.Subscribe.Group == null)
            {
                _log.Info("subscribing");
            }
        });
    }
}
