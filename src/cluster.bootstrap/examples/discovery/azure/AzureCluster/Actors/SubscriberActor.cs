// -----------------------------------------------------------------------
//  <copyright file="SubscriberActor.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Event;
using Akka.Util;

namespace KubernetesCluster.Actors
{
    public class SubscriberActor : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new SubscriberActor());
        
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public SubscriberActor()
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
}