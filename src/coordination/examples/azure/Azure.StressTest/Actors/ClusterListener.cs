using Akka.Event;

namespace Azure.StressTest.Actors
{
    public class ClusterListener : ReceiveActor
    {
        public static Props Props() => Akka.Actor.Props.Create(() => new ClusterListener());

        public ClusterListener()
        {
             var log = Context.GetLogger();

            var cluster = Cluster.Get(Context.System);
            cluster.Subscribe(
                Self, 
                ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents, 
                typeof(ClusterEvent.MemberStatusChange),
                typeof(ClusterEvent.ReachabilityEvent));

            Receive<ClusterEvent.ReachabilityEvent>(message =>
            {
                switch (message)
                {
                    case ClusterEvent.UnreachableMember msg:
                        log.Info($"Member detected as unreachable: {msg.Member}");
                        break;
                    case ClusterEvent.ReachableMember msg:
                        log.Info($"Member is now reachable: {msg.Member}");
                        break;
                    default:
                        Unhandled(message);
                        break;
                }
            });

            Receive<ClusterEvent.MemberStatusChange>(message =>
            {
                switch (message)
                {
                    case ClusterEvent.MemberUp msg:
                        log.Info($"Member is now Up: {msg.Member.Address}");
                        break;
                    case ClusterEvent.MemberRemoved msg:
                        log.Info($"Member is removed: {msg.Member.Address} after {msg.PreviousStatus}");
                        break;
                    default:
                        Unhandled(message);
                        break;
                }
            });
        }
    }
}
